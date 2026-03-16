using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using pqy_server.Data;
using pqy_server.Models.Order;
using pqy_server.Models.Orders;
using pqy_server.Services.EmailService;
using pqy_server.Shared;
using pqy_server.Services;
using System.Security.Cryptography;
using System.Text;
using DbOrder = pqy_server.Models.Orders.Order;
using Serilog;

namespace pqy_server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly RazorpaySettings _razorpaySettings;
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<OrderController> _logger;
        private readonly IRazorpayService _razorpayService;

        public OrderController(
            IOptions<RazorpaySettings> razorpayOptions, 
            AppDbContext context, 
            IEmailService emailService, 
            IMemoryCache cache,
            ILogger<OrderController> logger,
            IRazorpayService razorpayService)
        {
            _razorpaySettings = razorpayOptions.Value;
            _context = context;
            _emailService = emailService;
            _cache = cache;
            _logger = logger;
            _razorpayService = razorpayService;
        }

        [HttpGet("plans")]
        public IActionResult GetPlans()
        {
            var plans = PlanData.DefaultPlans
                .Select(p => new
                {
                    p.Id,
                    p.DurationLabel,
                    p.Amount,
                    p.OriginalAmount,
                    p.IsRecommended
                })
                .ToList();

            return Ok(ApiResponse<object>.Success(plans, "Plans fetched successfully."));
        }

        // 🔹 Create Order (One-time payment with record recycling)
        [HttpPost("create-order")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User not identified."));
            }

            // ✅ Validate plan
            var plan = PlanData.DefaultPlans.FirstOrDefault(p => p.Id == request.PlanId);
            if (plan == null)
            {
                Log.Warning("CreateOrder: Invalid plan. pid={pid} uid={uid}", request.PlanId, userId);
                return BadRequest(ApiResponse<string>.Failure(ResultCode.InvalidPlan, "Invalid plan selected."));
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable);

                try
                {
                    // ✅ Check for active premium plan FIRST - prevent overlapping purchases
                    var activePlan = await _context.Orders
                        .AsNoTracking()
                        .FirstOrDefaultAsync(o => o.UserId == userId
                                                  && o.Status == OrderStatus.Paid
                                                  && o.ExpiresAt > DateTime.UtcNow);

                    if (activePlan != null)
                    {
                        Log.Information("CreateOrder: Active plan exists. uid={uid}", userId);
                        return BadRequest(ApiResponse<string>.Failure(ResultCode.ActivePlanExists,
                            "You already have an active premium plan."));
                    }

                    // Safe unique receipt logic
                    var receipt = $"rcpt_{userId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}".ToSafeSubstring(0, 20);

                    var razorOrderId = _razorpayService.CreateOrder(plan.Amount, receipt);

                    // ✅ ♻️ RECYCLING LOGIC: Find an existing order for this user to update instead of creating a new row
                    var existingOrder = await _context.Orders
                        .FirstOrDefaultAsync(o => o.UserId == userId
                                                  && (o.Status == OrderStatus.Created
                                                      || o.Status == OrderStatus.Failed
                                                      || (o.Status == OrderStatus.Paid && o.ExpiresAt <= DateTime.UtcNow)));

                    if (existingOrder != null)
                    {
                        Log.Information("CreateOrder: Recycling order. oid={oid} uid={uid}",
                            existingOrder.Id, userId);

                        existingOrder.RazorpayOrderId = razorOrderId;
                        existingOrder.Amount = plan.Amount;
                        existingOrder.PlanId = plan.Id;
                        existingOrder.Status = OrderStatus.Created;
                        existingOrder.PaymentId = null;
                        existingOrder.AmountPaid = null;
                        existingOrder.ExpiresAt = null;
                        existingOrder.CompletedAt = null;
                        existingOrder.CreatedAt = DateTime.UtcNow;
                        existingOrder.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        Log.Information("CreateOrder: New order. uid={uid}", userId);

                        var newOrder = new DbOrder
                        {
                            UserId = userId,
                            RazorpayOrderId = razorOrderId,
                            Amount = plan.Amount,
                            Currency = "INR",
                            Status = OrderStatus.Created,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            PlanId = plan.Id
                        };
                        _context.Orders.Add(newOrder);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var result = new
                    {
                        orderId = razorOrderId,
                        amount = plan.Amount,
                        currency = "INR",
                        key = _razorpaySettings.KeyId
                    };

                    return (IActionResult)Ok(ApiResponse<object>.Success(result, "Order created successfully."));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Log.Error(ex, "CreateOrder failed. uid={uid}", userId);
                    return StatusCode(500,
                        ApiResponse<string>.Failure(ResultCode.OrderError,
                            "An error occurred while creating the order."));
                }
            });
        }


        // 🔹 Verify Order Payment
        [HttpPost("verify-order")]
        public async Task<IActionResult> VerifyOrder([FromBody] OrderVerification payload)
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(ApiResponse<bool>.Failure(ResultCode.Unauthorized, "User not identified."));
            }
            
            try
            {
                Log.Information("VerifyOrder called. uid={uid} oid={oid}", userId, payload.OrderId);

                // ✅ Signature verification
                string body = payload.OrderId + "|" + payload.PaymentId;
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_razorpaySettings.KeySecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                var generatedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(generatedSignature),
                    Encoding.UTF8.GetBytes(payload.Signature.ToLower())))
                {
                    Log.Warning("VerifyOrder: Sig mismatch. uid={uid} oid={oid}", userId, payload.OrderId);
                    return BadRequest(ApiResponse<bool>.Failure(ResultCode.InvalidSignature, "Invalid payment signature."));
                }

                // ✅ Look up order and check ownership (Security Hardening)
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.RazorpayOrderId == payload.OrderId);
                if (order == null)
                {
                    _logger.LogWarning("VerifyOrder: Order not found. oid={oid}", payload.OrderId);
                    return NotFound(ApiResponse<bool>.Failure(ResultCode.NotFound, "Order not found."));
                }

                if (order.UserId != userId)
                {
                    _logger.LogWarning("VerifyOrder: Security! uid={uid} tried oid={oid} owned by {owner}", 
                        userId, payload.OrderId, order.UserId);
                    return StatusCode(403, ApiResponse<bool>.Failure(ResultCode.OrderNotOwned, "You do not own this order."));
                }

                if (order.Status != OrderStatus.Paid)
                {
                    var plan = PlanData.DefaultPlans.FirstOrDefault(p => p.Id == order.PlanId);
                    if (plan == null)
                    {
                        Log.Error("VerifyOrder: Plan not found. oid={oid} pid={pid}", order.Id, order.PlanId);
                        return BadRequest(ApiResponse<bool>.Failure(ResultCode.InvalidPlan, "Invalid plan for this order."));
                    }

                    order.Status = OrderStatus.Paid;
                    order.PaymentId = payload.PaymentId;
                    order.AmountPaid = order.Amount; // Set from plan if captured
                    order.UpdatedAt = DateTime.UtcNow;
                    order.CompletedAt = DateTime.UtcNow;
                    order.ExpiresAt = DateTime.UtcNow.AddDays(plan.DurationInDays);

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("VerifyOrder: Paid. oid={oid} exp={exp}",
                         order.RazorpayOrderId, order.ExpiresAt);
                         
                    // Invalidate premium status cache
                    _cache.Remove(CacheKeys.UserPremiumStatus(order.UserId));
                }
                else
                {
                    _logger.LogInformation("VerifyOrder: Already paid (idempotent). oid={oid}", order.RazorpayOrderId);
                }

                return Ok(ApiResponse<bool>.Success(true, "Order payment verified."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "VerifyOrder failed. uid={uid} oid={oid}", userId, payload.OrderId);
                return StatusCode(500,
                    ApiResponse<bool>.Failure(ResultCode.OrderError, "An error occurred while verifying the order."));
            }
        }

        [HttpGet("my-plan")]
        public async Task<IActionResult> GetMyPlan()
        {
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(ApiResponse<MyPlanDto>.Failure(ResultCode.Unauthorized, "User not identified."));
            }

            try
            {
                var activeOrder = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.UserId == userId
                                && o.Status == OrderStatus.Paid
                                && (o.ExpiresAt != null && o.ExpiresAt > DateTime.UtcNow))
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                if (activeOrder == null)
                {
                    return Ok(ApiResponse<MyPlanDto>.Success(new MyPlanDto { IsPremium = false }, "No active plan found."));
                }

                var result = new MyPlanDto
                {
                    IsPremium = true,
                    ExpiresAt = activeOrder.ExpiresAt,
                    RemainingDays = activeOrder.RemainingDays,
                    OrderId = activeOrder.RazorpayOrderId,
                    AmountPaid = activeOrder.AmountPaid
                };

                return Ok(ApiResponse<MyPlanDto>.Success(result, "Active plan details fetched successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "my-plan fetch failed. uid={uid}", userId);
                return StatusCode(500,
                    ApiResponse<string>.Failure(ResultCode.OrderError, "An error occurred while fetching the plan."));
            }
        }


        // 🔹 Razorpay Webhook
        [AllowAnonymous]
        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                Request.EnableBuffering();
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
                var payload = await reader.ReadToEndAsync();
                Request.Body.Position = 0;

                _logger.LogInformation("Webhook received. len={len}", payload.Length);

                var receivedSignature = Request.Headers["X-Razorpay-Signature"].ToString();
                if (string.IsNullOrEmpty(receivedSignature))
                {
                    _logger.LogWarning("Webhook sig missing.");
                    return BadRequest(ApiResponse<string>.Failure(ResultCode.WebhookError, "Missing Razorpay signature."));
                }

                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_razorpaySettings.WebhookSecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                var generatedSignature = BitConverter.ToString(hash).Replace("-", "").ToLower();

                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(generatedSignature),
                    Encoding.UTF8.GetBytes(receivedSignature.ToLower())))
                {
                    _logger.LogWarning("Webhook sig mismatch.");
                    return BadRequest(ApiResponse<string>.Failure(ResultCode.WebhookError, "Invalid Razorpay webhook signature."));
                }

                var data = JsonConvert.DeserializeObject<RazorpayWebhookPayload>(payload);
                if (data == null)
                {
                    _logger.LogWarning("Webhook payload invalid.");
                    return BadRequest(ApiResponse<string>.Failure(ResultCode.WebhookError, "Empty or invalid payload."));
                }

                Log.Information("Webhook: {evt}", data.Event);

                switch (data.Event)
                {
                    case "order.paid":
                    case "payment.captured":
                        if (data.Payload.Payment != null)
                            await HandlePaymentCaptured(data.Payload.Payment.Entity);
                        break;
                    case "payment.failed":
                        if (data.Payload.Payment != null)
                            await HandlePaymentFailed(data.Payload.Payment.Entity);
                        break;
                    case "refund.created":
                        if (data.Payload.Refund != null)
                            await HandleRefund(data.Payload.Refund.Entity);
                        break;
                }

                return Ok(ApiResponse<string>.Success("Webhook processed successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Webhook failed.");
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.WebhookError, "An error occurred while processing the webhook."));
            }
        }

        // 🔹 Event Handlers
        private async Task HandlePaymentCaptured(RazorpayPaymentEntity entity)
        {
            Log.Information("Webhook captured. pyid={pyid}, oid={oid}", entity.Id, entity.Order_Id);

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.RazorpayOrderId == entity.Order_Id);
            if (order == null)
            {
                Log.Warning("Webhook: No order record. oid={oid}", entity.Order_Id);
                return;
            }

            if (order.Status != OrderStatus.Paid)
            {
                var plan = PlanData.DefaultPlans.FirstOrDefault(p => p.Id == order.PlanId);
                if (plan == null)
                {
                    Log.Error("Webhook: Plan not found. oid={oid}, pid={pid}", order.Id, order.PlanId);
                    return;
                }

                var receivedAmount = entity.Amount / 100;
                if (receivedAmount < plan.Amount)
                {
                    Log.Warning(
                        "Webhook: Amount mismatch. exp={exp}, rcv={rcv}, oid={oid}",
                        plan.Amount, receivedAmount, order.Id);
                    return;
                }

                order.Status = OrderStatus.Paid;
                order.PaymentId = entity.Id;
                order.AmountPaid = receivedAmount;
                order.UpdatedAt = DateTime.UtcNow;
                order.CompletedAt = DateTime.UtcNow;
                order.ExpiresAt = DateTime.UtcNow.AddDays(plan.DurationInDays);

                await _context.SaveChangesAsync();
                Log.Information("Webhook: Order paid. oid={oid}, exp={exp}", order.RazorpayOrderId, order.ExpiresAt);

                // Invalidate premium status cache
                _cache.Remove(CacheKeys.UserPremiumStatus(order.UserId));

                // 🔥 SEND SUCCESS EMAIL
                var user = await _context.Users.FindAsync(order.UserId);
                if (user != null)
                {
                    try
                    {
                        await _emailService.SendPaymentSuccessEmail(
                            user.UserEmail,
                            user.Username,
                            order.AmountPaid ?? order.Amount,
                            order.ExpiresAt.Value);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Webhook: Email failed. uid={uid}", user.UserId);
                    }
                }
            }
        }

        private async Task HandlePaymentFailed(RazorpayPaymentEntity entity)
        {
            Log.Information("Webhook failed. pyid={pyid}, oid={oid}", entity.Id, entity.Order_Id);

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.RazorpayOrderId == entity.Order_Id);
            if (order == null)
            {
                Log.Warning("Webhook: No order for failure. oid={oid}", entity.Order_Id);
                return;
            }

            // ✅ Status Guard: Only mark as failed if it's not already paid (prevents late fail webhooks from breaking access)
            if (order.Status == OrderStatus.Created)
            {
                order.Status = OrderStatus.Failed;
                order.UpdatedAt = DateTime.UtcNow;
                order.CompletedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                Log.Information("Webhook: Order failed. oid={oid}", order.RazorpayOrderId);

                var user = await _context.Users.FindAsync(order.UserId);
                if (user != null)
                {
                    try
                    {
                        await _emailService.SendPaymentFailureEmail(user.UserEmail, user.Username);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Webhook: Failure email fail. uid={uid}", user.UserId);
                    }
                }
            }
        }

        private async Task HandleRefund(RazorpayRefundEntity entity)
        {
            Log.Information("Webhook refund. rid={rid}, pyid={pyid}", entity.Id, entity.PaymentId);

            // Look up by PaymentId since refund webhooks don't always have the RazorpayOrderId at top level
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.PaymentId == entity.PaymentId);
            if (order == null)
            {
                Log.Warning("Webhook: No order for refund. pyid={pyid}", entity.PaymentId);
                return;
            }

            order.Status = OrderStatus.Refunded;
            order.UpdatedAt = DateTime.UtcNow;
            order.CompletedAt = DateTime.UtcNow;
            order.ExpiresAt = DateTime.UtcNow; // Immediately revoke premium

            await _context.SaveChangesAsync();
            Log.Information("Webhook: Order refunded. oid={oid}", order.RazorpayOrderId);

            // Invalidate premium status cache
            _cache.Remove(CacheKeys.UserPremiumStatus(order.UserId));
        }
    }

    public static class StringExtensions
    {
        public static string ToSafeSubstring(this string str, int startIndex, int length)
        {
            if (string.IsNullOrEmpty(str)) return str;
            return str.Length <= length ? str : str.Substring(startIndex, length);
        }
    }
}
