using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using pqy_server.Controllers;
using pqy_server.Data;
using pqy_server.Models.Order;
using pqy_server.Models.Orders;
using pqy_server.Models.Users;
using pqy_server.Services;
using pqy_server.Services.EmailService;
using pqy_server.Shared;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace pqy_server.UnitTests.Controllers
{
    public class OrderControllerTests
    {
        private readonly Mock<IEmailService> _emailServiceMock;
        private readonly Mock<IMemoryCache> _cacheMock;
        private readonly Mock<ILogger<OrderController>> _loggerMock;
        private readonly Mock<IRazorpayService> _razorpayServiceMock;
        private readonly IOptions<RazorpaySettings> _razorpayOptions;

        public OrderControllerTests()
        {
            _emailServiceMock = new Mock<IEmailService>();
            _cacheMock = new Mock<IMemoryCache>();
            _loggerMock = new Mock<ILogger<OrderController>>();
            _razorpayServiceMock = new Mock<IRazorpayService>();
            _razorpayOptions = Options.Create(new RazorpaySettings
            {
                KeyId = "test_key",
                KeySecret = "test_secret",
                WebhookSecret = "test_webhook_secret"
            });
        }

        private AppDbContext CreateDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new AppDbContext(options);
        }

        private OrderController CreateController(AppDbContext context, int userId = 1, string role = "User")
        {
            var controller = new OrderController(
                _razorpayOptions,
                context,
                _emailServiceMock.Object,
                _cacheMock.Object,
                _loggerMock.Object,
                _razorpayServiceMock.Object
            );

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            return controller;
        }

        [Fact]
        public async Task CreateOrder_ShouldRecycleOrder_WhenPendingExists()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateDbContext(dbName);
            var userId = 1;
            var planId = 1;
            
            // Add existing "Created" order
            context.Orders.Add(new Order
            {
                UserId = userId,
                PlanId = planId,
                Status = OrderStatus.Created,
                RazorpayOrderId = "old_razor_id",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10)
            });
            await context.SaveChangesAsync();

            _razorpayServiceMock.Setup(s => s.CreateOrder(It.IsAny<decimal>(), It.IsAny<string>()))
                .Returns("new_razor_id");

            var controller = CreateController(context, userId);

            // Act
            var result = await controller.CreateOrder(new CreateOrderRequest { PlanId = planId });

            // Assert
            var okResult = result.As<OkObjectResult>();
            var response = okResult.Value.As<ApiResponse<object>>();
            response.ResultCode.Should().Be(ResultCode.Success);

            var updatedOrder = await context.Orders.FirstOrDefaultAsync(o => o.UserId == userId);
            updatedOrder.Should().NotBeNull();
            updatedOrder.RazorpayOrderId.Should().Be("new_razor_id"); // Verified recycling (update existing row)
            context.Orders.Count().Should().Be(1);
        }

        [Fact]
        public async Task CreateOrder_ShouldFail_WhenActivePremiumExists()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateDbContext(dbName);
            var userId = 1;
            
            // Add active plan
            context.Orders.Add(new Order
            {
                UserId = userId,
                Status = OrderStatus.Paid,
                ExpiresAt = DateTime.UtcNow.AddDays(10)
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);

            // Act
            var result = await controller.CreateOrder(new CreateOrderRequest { PlanId = 1 });

            // Assert
            var badRequest = result.As<BadRequestObjectResult>();
            var response = badRequest.Value.As<ApiResponse<string>>();
            response.ResultCode.Should().Be(ResultCode.ActivePlanExists);
        }

        [Fact]
        public async Task VerifyOrder_ShouldFail_WhenOrderNotOwned()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateDbContext(dbName);
            var userId = 1;
            var ownerId = 2; // Different user
            var razorOrderId = "order_123";
            
            context.Orders.Add(new Order
            {
                UserId = ownerId,
                RazorpayOrderId = razorOrderId,
                Status = OrderStatus.Created,
                Amount = 500,
                PlanId = 1
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context, userId);
            
            var verificationRequest = new OrderVerification
            {
                OrderId = razorOrderId,
                PaymentId = "pay_123"
            };

            // Generate valid HMAC signature for the payload
            var key = Encoding.UTF8.GetBytes(_razorpayOptions.Value.KeySecret);
            var body = verificationRequest.OrderId + "|" + verificationRequest.PaymentId;
            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            verificationRequest.Signature = BitConverter.ToString(hash).Replace("-", "").ToLower();

            // Act
            var result = await controller.VerifyOrder(verificationRequest);

            // Assert
            var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
            objectResult.StatusCode.Should().Be(403);
            
            var errorResponse = objectResult.Value.As<ApiResponse<bool>>();
            errorResponse.ResultCode.Should().Be(ResultCode.OrderNotOwned);
        }

        [Fact]
        public async Task Webhook_Captured_ShouldUpdateOrderAndPremium()
        {
            // Arrange
            var dbName = Guid.NewGuid().ToString();
            using var context = CreateDbContext(dbName);
            var userId = 1;
            var razorOrderId = "order_123";
            var paymentId = "pay_123";
            
            context.Orders.Add(new Order
            {
                UserId = userId,
                RazorpayOrderId = razorOrderId,
                Status = OrderStatus.Created,
                Amount = 500,
                PlanId = 1
            });
            
            context.Users.Add(new User { UserId = userId, Username = "test", UserEmail = "test@example.com" });
            await context.SaveChangesAsync();

            var controller = CreateController(context);
            
            // MINIFIED Accurate Razorpay payload format (Crucial for signature match)
            var jsonPayload = "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_123\",\"order_id\":\"order_123\",\"amount\":50000,\"status\":\"captured\"}}}}";

            var requestStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonPayload));
            controller.Request.Body = requestStream;
            
            var key = Encoding.UTF8.GetBytes(_razorpayOptions.Value.WebhookSecret);
            using var hmac = new HMACSHA256(key);
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(jsonPayload));
            var signature = BitConverter.ToString(hash).Replace("-", "").ToLower();
            
            controller.Request.Headers["X-Razorpay-Signature"] = signature;

            // Act
            var result = await controller.Webhook();

            // Assert
            if (result is not OkObjectResult)
            {
                 if (result is ObjectResult obj)
                 {
                     var resp = obj.Value.As<ApiResponse<string>>();
                     throw new Exception($"Webhook failed with {obj.StatusCode}. Error: {resp?.Message}. ExpectedSig: {signature}. Payload: {jsonPayload}");
                 }
                 throw new Exception($"Webhook failed with {result.GetType().Name}");
            }
            
            var okResult = result.As<OkObjectResult>();
            var response = okResult.Value.As<ApiResponse<string>>();
            response.ResultCode.Should().Be(ResultCode.Success);
            
            var order = await context.Orders.FirstOrDefaultAsync(o => o.RazorpayOrderId == razorOrderId);
            order.Should().NotBeNull("Order should exist in DB");
            order!.Status.Should().Be(OrderStatus.Paid);
            order.PaymentId.Should().Be(paymentId);
        }
    }
}
