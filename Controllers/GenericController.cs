using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Exams;
using pqy_server.Models.Order;
using pqy_server.Models.Generic;
using pqy_server.Models.Notifications;
using pqy_server.Models.Quotes;
using pqy_server.Models.Subjects;
using pqy_server.Models.Topics;
using pqy_server.Models.User;
using pqy_server.Models.Year;
using pqy_server.Models.Years;
using pqy_server.Services;
using pqy_server.Shared;
using Serilog;
using System.Security.Claims;

namespace pqy_server.Controllers
{
    [Authorize] // 👤 All routes require an authenticated user
    [ApiController]
    [Route("api/[controller]")]
    public class GenericController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storage;
        private readonly MediaUrlBuilder _mediaUrl;
        private readonly ReorderService _reorderService;

        public GenericController(AppDbContext context, IStorageService storage, MediaUrlBuilder mediaUrl, ReorderService reorderService)
        {
            _context = context;
            _storage = storage;
            _mediaUrl = mediaUrl;
            _reorderService = reorderService;
        }

        [HttpGet]
        public async Task<IActionResult> GetInit()
        {
            bool isAdmin = User.IsInRole(RoleConstant.Admin);
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                    return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User not identified."));

                // ✅ Profile
                var user = await _context.Users
                    .AsNoTracking()
                    .Include(u => u.Role)
                    .FirstOrDefaultAsync(u => u.UserId == userId);

                if (user == null)
                    return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User not found."));

                var profile = new MyProfileDto
                {
                    UserId = user.UserId,
                    Username = user.Username,
                    UserEmail = user.UserEmail,
                    Role = user.Role?.RoleName ?? "Unknown",
                    CreatedAt = user.CreatedAt,
                    SelectedExamIds = user.SelectedExamIds
                };

                // ✅ Plan
                var activeOrder = await _context.Orders
                    .AsNoTracking()
                    .Where(o => o.UserId == userId
                            && o.Status == OrderStatus.Paid
                            && o.Status != OrderStatus.Refunded
                            && o.ExpiresAt != null
                            && o.ExpiresAt > DateTime.UtcNow)
                    .OrderByDescending(o => o.CreatedAt)
                    .FirstOrDefaultAsync();

                var myPlan = new MyPlanDto
                {
                    IsPremium = activeOrder != null && activeOrder.ExpiresAt > DateTime.UtcNow,
                    ExpiresAt = activeOrder?.ExpiresAt,
                    RemainingDays = activeOrder?.ExpiresAt != null
                        ? (activeOrder.ExpiresAt.Value - DateTime.UtcNow).Days
                        : (int?)null,
                    OrderId = activeOrder?.RazorpayOrderId,
                    AmountPaid = activeOrder?.AmountPaid
                };

                // Sync profile IsPremium from the order-derived plan
                profile.IsPremium = myPlan.IsPremium;

                // ✅ Other entities
                var exams = await _context.Exams
                    .AsNoTracking()
                    .Where(e => !e.IsDeleted)
                    .OrderBy(e => e.ExamOrder)
                    .Select(e => new Exam
                    {
                        ExamId = e.ExamId,
                        ExamName = e.ExamName,
                        ShortName = e.ShortName,
                        ExamOrder = e.ExamOrder,
                        IsDeleted = e.IsDeleted,
                    })
                    .ToListAsync();

                // ✅ Determine if the user has active premium access
                bool isPremiumUser = isAdmin || (myPlan.IsPremium && myPlan.ExpiresAt > DateTime.UtcNow);

                var years = await (
                    from y in _context.Years.AsNoTracking()
                    join q in _context.Questions
                        on y.YearId equals q.YearId into qg
                    // .Where(y => !y.IsDeleted)
                    orderby y.YearOrder descending
                    select new
                    {
                        YearId = y.YearId,
                        YearName = y.YearName,
                        PaperName = y.PaperName,
                        YearOrder = y.YearOrder,
                        ExamId = y.ExamId,
                        ExamName = y.Exam.ExamName,
                        IsPremium = y.IsPremium,
                        IsDeleted = y.IsDeleted,
                        QuestionCount = qg.Count(),
                        QuestionPaperKey = y.QuestionPaperKey,
                        AnswerKeyKey = y.AnswerKeyKey,
                    }
                ).ToListAsync();

                // Security fix: use presigned URLs (5-min expiry) instead of permanent public URLs
                var yearDtos = years.Select(y => new YearDto
                {
                    YearId = y.YearId,
                    YearName = y.YearName,
                    PaperName = y.PaperName,
                    YearOrder = y.YearOrder,
                    ExamId = y.ExamId,
                    ExamName = y.ExamName,
                    IsPremium = y.IsPremium,
                    IsDeleted = y.IsDeleted,
                    QuestionCount = y.QuestionCount,

                    // If the user is premium/admin, or it's a free year, return presigned URL.
                    // If free user and premium year: return "locked" when a PDF exists, null otherwise.
                    QuestionPaperUrl = (isPremiumUser || !y.IsPremium)
                        ? (y.QuestionPaperKey != null ? _storage.GetPresignedFileUrl(y.QuestionPaperKey) : null)
                        : (y.QuestionPaperKey != null ? "locked" : null),

                    AnswerKeyUrl = (isPremiumUser || !y.IsPremium)
                        ? (y.AnswerKeyKey != null ? _storage.GetPresignedFileUrl(y.AnswerKeyKey) : null)
                        : (y.AnswerKeyKey != null ? "locked" : null)
                }).ToList();

                IQueryable<Subject> subjectQuery = _context.Subjects.AsNoTracking();

                // 🚫 Hide deleted subjects for non-admins
                if (!isAdmin)
                {
                    subjectQuery = subjectQuery.Where(s => !s.IsDeleted);
                }

                var subjects = await subjectQuery
                    .OrderBy(s => s.SubjectOrder)
                    .Select(s => new Subject
                    {
                        SubjectId = s.SubjectId,
                        SubjectName = s.SubjectName,
                        SubjectOrder = s.SubjectOrder,
                        IsDeleted = s.IsDeleted,
                    })
                    .ToListAsync();


                var userSelectedExamIds = user.SelectedExamIds ?? new List<int>();

                var topics = await (
                    from t in _context.Topics
                    join s in _context.Subjects on t.SubjectId equals s.SubjectId
                    let count = _context.Questions.Count(q =>
                        q.TopicId == t.TopicId &&
                        q.SubjectId == t.SubjectId &&
                        (isAdmin || (q.ExamId != null && userSelectedExamIds.Contains(q.ExamId.Value))) &&
                        (isPremiumUser || (q.Year != null && !q.Year.IsPremium)))
                    where isAdmin || count > 0
                    orderby t.TopicOrder
                    select new Topic
                    {
                        TopicId = t.TopicId,
                        TopicName = t.TopicName,
                        SubjectId = t.SubjectId,
                        TopicOrder = t.TopicOrder,
                        SubjectName = s.SubjectName,
                        QuestionCount = count
                    }
                ).ToListAsync();

                var notifications = await _context.Notifications
                    .AsNoTracking()
                    .Where(n => n.UserId == null || n.UserId == userId)
                    .OrderByDescending(n => n.CreatedAt)
                    .Select(n => new Notification
                    {
                        NotificationId = n.NotificationId,
                        UserId = n.UserId,
                        Title = n.Title,
                        Message = n.Message,
                        IsRead = n.IsRead,
                        CreatedAt = n.CreatedAt
                    })
                    .ToListAsync();

                var quotes = await _context.Quotes
                    .AsNoTracking()
                    .OrderByDescending(q => q.CreatedAt)
                    .ToListAsync();

                var result = quotes.Select(q => new pqy_server.Models.Quotes.Quote
                {
                    Id = q.Id,
                    Text = q.Text,
                    Author = q.Author,
                    ImageUrl = string.IsNullOrEmpty(q.ImageUrl)
                        ? null
                        : _mediaUrl.Build(q.ImageUrl), // use presigned URL
                    CreatedAt = q.CreatedAt
                }).ToList();


                // ✅ Assemble Init DTO
                var initData = new Init
                {
                    Profile = profile,
                    MyPlan = myPlan,
                    Exams = exams,
                    Years = yearDtos,
                    Subjects = subjects,
                    Topics = topics,
                    Notifications = notifications,
                    Quotes = result
                };

                return Ok(ApiResponse<Init>.Success(initData, "Init data fetched successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Init data failed. uid={uid}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                return StatusCode(500,
                    ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while fetching init data."));
            }
        }
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPut("reorder")]
        public async Task<IActionResult> Reorder([FromBody] ReorderRequest request)
        {
            switch (request.ItemType)
            {
                case ReorderItemType.Exam:
                    {
                        var exam = await _context.Exams.FindAsync(request.ItemId);
                        if (exam == null)
                            return NotFound(ApiResponse<string>.Failure(
                                ResultCode.NotFound, "Exam not found."));

                        await _reorderService.ReorderAsync(
                            _context.Exams.OrderBy(e => e.ExamOrder),
                            exam,
                            request.NewOrder
                        );
                        break;
                    }

                case ReorderItemType.Subject:
                    {
                        var subject = await _context.Subjects.FindAsync(request.ItemId);
                        if (subject == null)
                            return NotFound(ApiResponse<string>.Failure(
                                ResultCode.NotFound, "Subject not found."));

                        // ✅ SUBJECT IS GLOBAL (NO ExamId)
                        await _reorderService.ReorderAsync(
                            _context.Subjects.OrderBy(s => s.SubjectOrder),
                            subject,
                            request.NewOrder
                        );
                        break;
                    }

                case ReorderItemType.Year:
                    {
                        var year = await _context.Years.FindAsync(request.ItemId);
                        if (year == null)
                            return NotFound(ApiResponse<string>.Failure(
                                ResultCode.NotFound, "Year not found."));

                        // ✅ Year is scoped per Exam
                        await _reorderService.ReorderAsync(
                            _context.Years
                                .Where(y => y.ExamId == year.ExamId)
                                .OrderBy(y => y.YearOrder),
                            year,
                            request.NewOrder
                        );
                        break;
                    }

                case ReorderItemType.Topic:
                    {
                        var topic = await _context.Topics.FindAsync(request.ItemId);
                        if (topic == null)
                            return NotFound(ApiResponse<string>.Failure(
                                ResultCode.NotFound, "Topic not found."));

                        // ✅ Topic is scoped per Subject
                        await _reorderService.ReorderAsync(
                            _context.Topics
                                .Where(t => t.SubjectId == topic.SubjectId)
                                .OrderBy(t => t.TopicOrder),
                            topic,
                            request.NewOrder
                        );
                        break;
                    }

                default:
                    return BadRequest(ApiResponse<string>.Failure(
                        ResultCode.ValidationError, "Invalid reorder type."));
            }

            return Ok(ApiResponse<string>.Success("Reordered successfully."));
        }

    }
}

