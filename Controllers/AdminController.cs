using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Helpers;
using pqy_server.Models.Admin;
using pqy_server.Models.Order;
using pqy_server.Models.Users;
using pqy_server.Services;
using pqy_server.Services.EmailService;
using pqy_server.Shared;
using Serilog;
using System.Security.Claims;

namespace pqy_server.Controllers
{
    [Authorize(Roles = RoleConstant.Admin)] // 🔐 All routes here require Admin role
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly INotificationService _notificationService; // ✅ Dependency injection for notifications
        private readonly IEmailService _emailService;
        private readonly ILogger<EmailService> _logger;

        public AdminController(AppDbContext context, INotificationService notificationService, IEmailService emailService, ILogger<EmailService> logger)
        {
            _context = context;
            _notificationService = notificationService;
            _emailService = emailService;
            _logger = logger;
        }

        // 🎓 POST: /api/admin/change-role
        [HttpPost("change-role")]
        public async Task<IActionResult> ChangeUserRole(ChangeUserRoleRequest request)
        {
            // Validate input
            if (request.UserId == 0 || request.NewRoleId == 0)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Invalid request."));

            // Find user by ID
            var user = await _context.Users.FindAsync(request.UserId);
            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            // Check if role is unchanged
            if (user.RoleId == request.NewRoleId)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "User already has this role."));

            // Validate new role exists
            var role = await _context.Roles.FindAsync(request.NewRoleId);
            if (role == null)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Invalid role."));

            // 🛑 Prevent self-role-change to avoid accidental demotion
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (currentUserId == user.UserId.ToString())
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Admins cannot change their own role."));

            // Update role and audit
            user.RoleId = request.NewRoleId;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success($"Role updated: {user.Username} → {role.RoleName}"));
        }

        // 📋 GET: /api/admin/users
        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 200) pageSize = 50;

            var totalCount = await _context.Users.CountAsync();

            var users = await _context.Users
                .Include(u => u.Role)
                .OrderBy(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.UserEmail,
                    Role = u.Role != null ? u.Role.RoleName : "Unknown",
                    IsPremium = _context.Orders.Any(o => o.UserId == u.UserId
                        && o.Status == OrderStatus.Paid
                        && o.ExpiresAt != null
                        && o.ExpiresAt > DateTime.UtcNow),
                    u.CreatedAt,
                    u.UpdatedAt,
                    u.SelectedExamIds,
                    u.IsDeleted
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Paginated(users, totalCount, page, pageSize));
        }

        // ➕ POST: /api/admin/users
        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.UserEmail) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Username, email, and password are required."));
            }

            // Check for existing email
            if (await _context.Users.AnyAsync(u => u.UserEmail == request.UserEmail))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Email already exists."));

            // Validate role
            var role = await _context.Roles.FindAsync(request.RoleId);
            if (role == null)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Invalid role."));

            // Hash the password securely
            using var hmac = new System.Security.Cryptography.HMACSHA512();
            var passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(request.Password));
            var passwordSalt = hmac.Key;

            var user = new User
            {
                Username = request.Username,
                UserEmail = request.UserEmail,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                RoleId = request.RoleId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                SelectedExamIds = request.SelectedExamIds,
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            Log.Information("User created by admin. uid={uid} usr={usr} eml={eml}", user.UserId, user.Username, user.UserEmail);

            return Ok(ApiResponse<object>.Success(new { user.UserId }, "User created successfully"));
        }

        // HARD DELETE — permanently remove from DB
        [HttpDelete("users/{id}/permanent")]
        public async Task<IActionResult> PermanentlyDeleteUser(int id)
        {
            var user = await _context.Users
                .IgnoreQueryFilters() // important – fetch even soft-deleted users
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            Log.Warning("User hard deleted. uid={uid}", id);

            return Ok(ApiResponse<string>.Success("User permanently deleted"));
        }

        // ❌ DELETE: /api/admin/users/{id}
        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            if (user.IsDeleted)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "User is already deleted."));

            // Soft delete
            user.IsDeleted = true;
            await _context.SaveChangesAsync();

            Log.Information("User soft deleted. uid={uid}", id);

            return Ok(ApiResponse<string>.Success("User soft deleted"));
        }

        // 🔄 Restore soft-deleted user: POST /api/admin/{id}/restore
        [HttpPost("{id}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "User not found."));

            if (!user.IsDeleted)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "User is not deleted."));

            user.IsDeleted = false;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("User restored"));
        }

        // 📊 GET: /api/admin/stats
        [HttpGet("stats")]
        public async Task<IActionResult> GetAdminStats()
        {
            // ================================
            // BASIC COUNTS
            // ================================
            var totalUsers = await _context.Users.CountAsync();
            var totalExams = await _context.Exams.CountAsync();
            var totalPyqs = await _context.Questions.CountAsync();
            var totalSubjects = await _context.Subjects.CountAsync();
            var totalTopics = await _context.Topics.CountAsync();
            var totalYears = await _context.Years.CountAsync();
            var premiumUsers = await _context.Orders
                .Where(o => o.Status == OrderStatus.Paid && o.ExpiresAt != null && o.ExpiresAt > DateTime.UtcNow)
                .Select(o => o.UserId).Distinct().CountAsync();

            var minDate = await _context.Questions
                .Where(q => !q.IsDeleted)
                .MinAsync(q => (DateTime?)q.UpdatedAt);

            if (minDate == null)
            {
                minDate = DateTime.UtcNow.Date;
            }

            var startDate = minDate.Value.Date;
            var endDate = DateTime.UtcNow.Date;

            var rawDailyCounts = await _context.Questions
                .Where(q => !q.IsDeleted)
                .GroupBy(q => q.UpdatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            var dailyUpdates = new List<DailyUpdateStat>();

            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                var match = rawDailyCounts.FirstOrDefault(x => x.Date == date);

                dailyUpdates.Add(new DailyUpdateStat
                {
                    Date = date,
                    Count = match?.Count ?? 0
                });
            }

            var minUpdateDate = await _context.Questions
                .Where(q => !q.IsDeleted)
                .MinAsync(q => (DateTime?)q.UpdatedAt);

            if (minUpdateDate == null)
            {
                minUpdateDate = DateTime.UtcNow;
            }

            var startMonth = new DateTime(minUpdateDate.Value.Year, minUpdateDate.Value.Month, 1);
            var endMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

            var rawMonthlyCounts = await _context.Questions
                .Where(q => !q.IsDeleted)
                .GroupBy(q => new
                {
                    q.UpdatedAt.Year,
                    q.UpdatedAt.Month
                })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .ToListAsync();

            var monthlyUpdates = new List<MonthlyUpdateStat>();

            for (var date = startMonth; date <= endMonth; date = date.AddMonths(1))
            {
                var match = rawMonthlyCounts.FirstOrDefault(x =>
                    x.Year == date.Year && x.Month == date.Month);

                monthlyUpdates.Add(new MonthlyUpdateStat
                {
                    Year = date.Year,
                    Month = date.Month,
                    Count = match?.Count ?? 0
                });
            }


            // ================================
            // ROLE STATS
            // ================================
            var roleCounts = await _context.Users
                .GroupBy(u => u.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToListAsync();

            var roles = await _context.Roles.ToListAsync();

            var result = roleCounts.Select(rc => new
            {
                name = roles.FirstOrDefault(r => r.RoleId == rc.RoleId)?.RoleName ?? "Unknown",
                count = rc.Count
            });


            // ================================
            // EXAM ANALYSIS — batch pre-aggregation to avoid N+1
            // ================================

            // All questions grouped by ExamId
            var examQStats = await _context.Questions
                .Where(q => !q.IsDeleted)
                .GroupBy(q => q.ExamId)
                .Select(g => new
                {
                    ExamId = g.Key,
                    Total = g.Count(),
                    WithExplanation = g.Count(q => q.Explanation != null &&
                                                   q.Explanation.Trim() != "" &&
                                                   q.Explanation.Trim().ToLower() != "null" &&
                                                   q.Explanation.Trim().ToLower() != "[null]"),
                    WithOfficialAnswer = g.Count(q => q.IsOfficialAnswer),
                })
                .ToListAsync();

            // All questions grouped by YearId
            var yearQStats = await _context.Questions
                .Where(q => !q.IsDeleted)
                .GroupBy(q => q.YearId)
                .Select(g => new
                {
                    YearId = g.Key,
                    Total = g.Count(),
                    WithExplanation = g.Count(q => q.Explanation != null &&
                                                   q.Explanation.Trim() != "" &&
                                                   q.Explanation.Trim().ToLower() != "null" &&
                                                   q.Explanation.Trim().ToLower() != "[null]"),
                    WithOfficialAnswer = g.Count(q => q.IsOfficialAnswer),
                    MissingExplanation = g.Count(q => q.Explanation == null ||
                                                      q.Explanation.Trim() == "" ||
                                                      q.Explanation.Trim().ToLower() == "null" ||
                                                      q.Explanation.Trim().ToLower() == "[null]"),
                    MissingOfficialAnswer = g.Count(q => !q.IsOfficialAnswer),
                })
                .ToListAsync();

            // Year count per exam
            var yearCountByExam = await _context.Years
                .GroupBy(y => y.ExamId)
                .Select(g => new { ExamId = g.Key, Count = g.Count() })
                .ToListAsync();

            var allExams = await _context.Exams
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.ExamOrder)
                .ToListAsync();

            var allYears = await _context.Years
                .OrderByDescending(y => y.YearOrder)
                .ToListAsync();

            var examAnalysis = allExams.Select(e =>
            {
                var eqs = examQStats.FirstOrDefault(x => x.ExamId == e.ExamId);
                var years = allYears
                    .Where(y => y.ExamId == e.ExamId)
                    .OrderByDescending(y => y.YearOrder)
                    .Select(y =>
                    {
                        var yqs = yearQStats.FirstOrDefault(x => x.YearId == y.YearId);
                        int total = yqs?.Total ?? 0;
                        int missingExpl = yqs?.MissingExplanation ?? 0;
                        int missingOff = yqs?.MissingOfficialAnswer ?? 0;
                        return new
                        {
                            y.YearId,
                            y.YearName,
                            TotalQuestions = total,
                            QuestionsWithExplanation = yqs?.WithExplanation ?? 0,
                            QuestionsWithOfficialAnswers = yqs?.WithOfficialAnswer ?? 0,
                            MissingExplanation = missingExpl,
                            MissingOfficial = missingOff,
                            AllHaveExplanation = total > 0 && missingExpl == 0,
                            AllHaveOfficialAnswers = total > 0 && missingOff == 0,
                        };
                    })
                    .ToList();

                return new
                {
                    e.ExamId,
                    e.ExamName,
                    e.ExamOrder,
                    YearCount = yearCountByExam.FirstOrDefault(x => x.ExamId == e.ExamId)?.Count ?? 0,
                    TotalQuestions = eqs?.Total ?? 0,
                    QuestionsWithExplanation = eqs?.WithExplanation ?? 0,
                    QuestionsWithOfficialAnswers = eqs?.WithOfficialAnswer ?? 0,
                    Years = years,
                };
            }).ToList();


            // ================================
            // SUBJECT STATS — batch pre-aggregation
            // ================================

            // All questions grouped by SubjectId
            var subjectQStats = await _context.Questions
                .Where(q => !q.IsDeleted)
                .GroupBy(q => q.SubjectId)
                .Select(g => new
                {
                    SubjectId = g.Key,
                    Total = g.Count(),
                    WithExplanation = g.Count(q => q.Explanation != null &&
                                                   q.Explanation.Trim() != "" &&
                                                   q.Explanation.Trim().ToLower() != "null" &&
                                                   q.Explanation.Trim().ToLower() != "[null]"),
                    WithOfficialAnswer = g.Count(q => q.IsOfficialAnswer),
                })
                .ToListAsync();

            var allSubjects = await _context.Subjects
                .Where(s => !s.IsDeleted)
                .OrderBy(s => s.SubjectOrder)
                .ToListAsync();

            var subjectStats = allSubjects.Select(s =>
            {
                var sqs = subjectQStats.FirstOrDefault(x => x.SubjectId == s.SubjectId);
                return new
                {
                    s.SubjectId,
                    s.SubjectName,
                    TotalQuestions = sqs?.Total ?? 0,
                    QuestionsWithExplanation = sqs?.WithExplanation ?? 0,
                    QuestionsWithOfficialAnswers = sqs?.WithOfficialAnswer ?? 0,
                };
            }).ToList();



            // ====================================
            // 🆕 1. GLOBAL MISSING EXPLANATION
            // ====================================
            var globalMissingExplanation = await _context.Questions
                .CountAsync(q => !q.IsDeleted &&
                    (q.Explanation == null ||
                     q.Explanation.Trim() == "" ||
                     q.Explanation.Trim().ToLower() == "null" ||
                     q.Explanation.Trim().ToLower() == "[null]"));

            // ====================================
            // 🆕 2. GLOBAL MISSING OFFICIAL ANSWERS
            // ====================================
            var globalMissingOfficialAnswers = await _context.Questions
                .CountAsync(q => !q.IsDeleted && !q.IsOfficialAnswer);


            // ====================================
            // 🆕 3. YEAR COVERAGE SUMMARY
            // ====================================
            var allYearsCoverage = await _context.Years
                .Where(y => !y.IsDeleted)
                .OrderBy(y => y.ExamId).ThenBy(y => y.YearOrder)
                .ToListAsync();

            // reuse yearQStats already fetched above
            var yearCoverageStats = allYearsCoverage.Select(y =>
            {
                var yqs = yearQStats.FirstOrDefault(x => x.YearId == y.YearId);
                return new
                {
                    y.YearId,
                    y.YearName,
                    y.ExamId,
                    TotalQuestions = yqs?.Total ?? 0,
                    MissingExplanation = yqs?.MissingExplanation ?? 0,
                    MissingOfficialAnswer = yqs?.MissingOfficialAnswer ?? 0,
                };
            }).ToList();


            // ================================
            // FINAL RESPONSE
            // ================================
            return Ok(ApiResponse<AdminStats>.Success(new AdminStats
            {
                TotalUsers = totalUsers,
                TotalExams = totalExams,
                TotalPyqs = totalPyqs,
                TotalSubjects = totalSubjects,
                TotalTopics = totalTopics,
                TotalYears = totalYears,
                PremiumUsers = premiumUsers,

                Roles = result,
                ExamAnalysis = examAnalysis,
                SubjectStats = subjectStats,

                GlobalMissingExplanation = globalMissingExplanation,
                GlobalMissingOfficialAnswers = globalMissingOfficialAnswers,

                YearCoverageStats = yearCoverageStats,

                DailyUpdates = dailyUpdates,
                MonthlyUpdates = monthlyUpdates
            }));
        }

        // 📊 GET: /api/admin/user-analytics
        [HttpGet("user-analytics")]
        public async Task<IActionResult> GetUserAnalytics()
        {
            try
            {
                var now = DateTime.UtcNow;
                var nowIst = IstHelper.NowIst();
                var today = DateOnly.FromDateTime(nowIst);
                var last30Days = today.AddDays(-30);
                var last7Days = today.AddDays(-7);

                // Daily active users — last 30 days
                var dailyActiveRaw = await _context.UserDailyProgress
                    .Where(p => p.Date >= last30Days)
                    .GroupBy(p => p.Date)
                    .Select(g => new { Date = g.Key, Value = g.Select(p => p.UserId).Distinct().Count() })
                    .OrderBy(g => g.Date)
                    .ToListAsync();

                // Daily practice volume — last 30 days
                var dailyVolumeRaw = await _context.UserDailyProgress
                    .Where(p => p.Date >= last30Days)
                    .GroupBy(p => p.Date)
                    .Select(g => new { Date = g.Key, Value = g.Sum(p => p.Attempts) })
                    .OrderBy(g => g.Date)
                    .ToListAsync();

                // Subject analytics — all time
                var subjectRaw = await _context.UserDailyProgress
                    .GroupBy(p => p.SubjectId)
                    .Select(g => new
                    {
                        SubjectId = g.Key,
                        TotalAttempts = g.Sum(p => p.Attempts),
                        TotalCorrect = g.Sum(p => p.Correct)
                    })
                    .ToListAsync();

                var subjects = await _context.Subjects
                    .Where(s => !s.IsDeleted)
                    .ToListAsync();

                var subjectAnalytics = subjectRaw
                    .Select(s => new
                    {
                        SubjectName = subjects.FirstOrDefault(sub => sub.SubjectId == s.SubjectId)?.SubjectName ?? "Unknown",
                        s.TotalAttempts,
                        s.TotalCorrect,
                        Accuracy = s.TotalAttempts > 0
                            ? Math.Round((double)s.TotalCorrect / s.TotalAttempts * 100, 2) : 0
                    })
                    .OrderByDescending(s => s.TotalAttempts)
                    .ToList();

                // Exam analytics — all time
                var examRaw = await _context.UserDailyProgress
                    .GroupBy(p => p.ExamId)
                    .Select(g => new
                    {
                        ExamId = g.Key,
                        TotalAttempts = g.Sum(p => p.Attempts),
                        TotalCorrect = g.Sum(p => p.Correct)
                    })
                    .ToListAsync();

                var exams = await _context.Exams
                    .Where(e => !e.IsDeleted)
                    .ToListAsync();

                var examAnalytics = examRaw
                    .Select(e => new
                    {
                        ExamName = exams.FirstOrDefault(ex => ex.ExamId == e.ExamId)?.ExamName ?? "Unknown",
                        e.TotalAttempts,
                        e.TotalCorrect,
                        Accuracy = e.TotalAttempts > 0
                            ? Math.Round((double)e.TotalCorrect / e.TotalAttempts * 100, 2) : 0
                    })
                    .OrderByDescending(e => e.TotalAttempts)
                    .ToList();

                // At-risk users: had activity before 7 days ago, nothing since
                var recentUserIds = await _context.UserDailyProgress
                    .Where(p => p.Date >= last7Days)
                    .Select(p => p.UserId)
                    .Distinct()
                    .ToListAsync();

                var atRiskUsers = await _context.UserDailyProgress
                    .Where(p => p.Date < last7Days && !recentUserIds.Contains(p.UserId))
                    .Select(p => p.UserId)
                    .Distinct()
                    .CountAsync();

                var totalActiveUsers30d = await _context.UserDailyProgress
                    .Where(p => p.Date >= last30Days)
                    .Select(p => p.UserId)
                    .Distinct()
                    .CountAsync();

                // New signups per day — last 30 days
                var signupsRaw = await _context.Users
                    .Where(u => !u.IsDeleted && u.CreatedAt >= now.AddDays(-30))
                    .GroupBy(u => DateOnly.FromDateTime(u.CreatedAt))
                    .Select(g => new { Date = g.Key, Value = g.Count() })
                    .OrderBy(g => g.Date)
                    .ToListAsync();

                // Premium stats
                var totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
                var premiumUsers = await _context.Orders
                    .Where(o => o.Status == OrderStatus.Paid && o.ExpiresAt != null && o.ExpiresAt > DateTime.UtcNow
                        && !o.User.IsDeleted)
                    .Select(o => o.UserId).Distinct().CountAsync();

                // Daily revenue — last 30 days (paid orders only)
                var revenueRaw = await _context.Orders
                    .Where(o => o.Status == OrderStatus.Paid && o.CompletedAt.HasValue
                        && o.CompletedAt.Value >= now.AddDays(-30))
                    .GroupBy(o => DateOnly.FromDateTime(o.CompletedAt!.Value))
                    .Select(g => new { Date = g.Key, Value = g.Sum(o => o.AmountPaid ?? o.Amount) })
                    .OrderBy(g => g.Date)
                    .ToListAsync();

                var totalRevenue = await _context.Orders
                    .Where(o => o.Status == OrderStatus.Paid)
                    .SumAsync(o => (long)(o.AmountPaid ?? o.Amount));

                // Exam mode analytics
                var examProgressData = await _context.ExamProgress.ToListAsync();
                var totalExams = examProgressData.Count;
                var completedExams = examProgressData.Count(e => e.CompletedAt.HasValue);
                var completionRate = totalExams > 0
                    ? Math.Round((double)completedExams / totalExams * 100, 1) : 0;

                var modeDistribution = examProgressData
                    .GroupBy(e => e.ModeType ?? "Unknown")
                    .Select(g => new { Mode = g.Key, Count = g.Count() })
                    .OrderByDescending(g => g.Count)
                    .ToList();

                var questionCountDistribution = examProgressData
                    .GroupBy(e => e.QuestionCount)
                    .Select(g => new { Count = g.Key, Sessions = g.Count() })
                    .OrderBy(g => g.Count)
                    .ToList();

                return Ok(ApiResponse<object>.Success(new
                {
                    DailyActiveUsers = dailyActiveRaw.Select(d => new { d.Date, d.Value }),
                    DailyVolume = dailyVolumeRaw.Select(d => new { d.Date, d.Value }),
                    SubjectAnalytics = subjectAnalytics,
                    ExamAnalytics = examAnalytics,
                    AtRiskUsers = atRiskUsers,
                    TotalActiveUsers30d = totalActiveUsers30d,
                    DailySignups = signupsRaw.Select(d => new { d.Date, d.Value }),
                    TotalUsers = totalUsers,
                    PremiumUsers = premiumUsers,
                    DailyRevenue = revenueRaw.Select(d => new { d.Date, d.Value }),
                    TotalRevenue = totalRevenue,
                    ExamCompletionRate = completionRate,
                    TotalExamSessions = totalExams,
                    ModeDistribution = modeDistribution,
                    QuestionCountDistribution = questionCountDistribution
                }, "User analytics generated successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "User analytics failed. aid={aid}", User.FindFirstValue(ClaimTypes.NameIdentifier));
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "Failed to generate user analytics."));
            }
        }

        // 📋 GET: /api/admin/reports
        [HttpGet("reports")]
        public async Task<IActionResult> GetAllReports()
        {
            var reports = await _context.QuestionReports
                .Include(r => r.Question)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt) // Order latest first
                .Select(r => new
                {
                    reportId = r.Id,
                    QuestionId = r.QuestionId,
                    r.WrongAnswer,
                    r.WrongExplanation,
                    r.WrongOptions,
                    r.QuestionFormatting,
                    r.DuplicateQuestion,
                    r.OtherDetails,
                    ReportedBy = r.User != null ? r.User.Username : "Unknown",
                    ReportedByUserId = r.User != null ? r.User.UserId : (int?)null,
                    r.CreatedAt,
                    r.IsResolved
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(reports));
        }


        // 🔢 GET: /api/admin/reports/unresolved-count
        [HttpGet("reports/unresolved-count")]
        public async Task<IActionResult> GetUnresolvedReportsCount()
        {
            var count = await _context.QuestionReports.CountAsync(r => !r.IsResolved);
            return Ok(ApiResponse<object>.Success(new { count }));
        }

        // ✅ POST: /api/admin/reports/{id}/resolve - Mark report as resolved
        [HttpPost("reports/{id}/resolve")]
        public async Task<IActionResult> ResolveReport(int id)
        {
            var report = await _context.QuestionReports
                .Include(r => r.User)
                .Include(r => r.Question)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (report == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, $"Report with id {id} not found."));

            try
            {
                // Mark as resolved
                report.IsResolved = true;
                await _context.SaveChangesAsync();

                if (report.User != null && !string.IsNullOrEmpty(report.User.UserEmail))
                {
                    try
                    {
                        await _emailService.SendReportResolvedEmail(
                            report.User.UserEmail,
                            report.User.Username,
                            report.QuestionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send report resolved email.");
                    }
                }

                string notifyMessage = "No user notification (user missing).";

                // Notify user if exists
                if (report.User != null)
                {
                    try
                    {
                        await _notificationService.SendNotificationToUserAsync(
                            report.User.UserId,
                            "Report Resolved",
                            $"Your report for question '{report.QuestionId}' has been resolved by admin."
                        );
                        notifyMessage = $"Notification sent to user {report.User.Username}.";
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Notify user failed after report. uid={uid}", report.User.UserId);
                    }
                }

                return Ok(ApiResponse<object>.Success(new
                {
                    message = report.Question != null
                        ? $"Report for question '{report.Question.QuestionText}' resolved."
                        : $"Report resolved (question {report.QuestionId} missing).",
                    notify = notifyMessage
                }));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Report resolve failed. rid={rid}", id);
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "Failed to resolve report."));
            }
        }

    }
}
