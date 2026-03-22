using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using pqy_server.Constants;
using pqy_server.Hubs;
using pqy_server.Data;
using pqy_server.Enums;
using pqy_server.Models.Images;
using pqy_server.Models.Questions;
using pqy_server.Models.Order;
using pqy_server.Services;
using pqy_server.Services.EmailService;
using pqy_server.Shared;
using System.Security.Claims;
using System.Threading;
using Serilog;
using static pqy_server.Enums.QuestionEnums;

namespace pqy_server.Controllers
{
    [Authorize] // 👤 Any authenticated user
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storage;
        private readonly MediaUrlBuilder _mediaUrl;
        private readonly ILogger<EmailService> _logger;
        private readonly IEmailService _emailService;
        private readonly IHubContext<AdminHub> _adminHub;
        private readonly IMemoryCache _cache;

        public QuestionsController(
            AppDbContext context,
            IStorageService storage,
            MediaUrlBuilder mediaUrl,
            IEmailService emailService,
            ILogger<EmailService> logger,
            IHubContext<AdminHub> adminHub,
            IMemoryCache cache)
        {
            _context = context;
            _storage = storage;
            _mediaUrl = mediaUrl;
            _emailService = emailService;
            _logger = logger;
            _adminHub = adminHub;
            _cache = cache;
        }

        /// <summary>
        /// GET: /api/questions
        /// Returns a basic list of questions (paged).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Get([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            // Redirect to filter logic for consistency
            return await Filter(null, null, null, null, null, null, page, pageSize, true, false);
        }

        /// <summary>
        /// GET: /api/questions/{id}
        /// Returns detailed information about a single question.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await GetQuestions(id);

            if (result == null)
                return NotFound(
                    ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found.")
                );

            return Ok(
                ApiResponse<object>.Success(result)
            );
        }


        /// <summary>
        /// PUT: /api/questions/{id}
        /// Updates an existing question. Admin only.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> UpdateQuestion(
            int id,
            [FromBody] UpdateQuestionRequest request)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question == null)
                return NotFound(
                    ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found.")
                );

            // -------------------------------
            // Validate foreign keys
            // -------------------------------
            if (request.SubjectId.HasValue &&
                !await _context.Subjects.AnyAsync(s => s.SubjectId == request.SubjectId))
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest, "Invalid SubjectId"));

            if (request.TopicId.HasValue &&
                !await _context.Topics.AnyAsync(t => t.TopicId == request.TopicId))
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest, "Invalid TopicId"));

            if (request.ExamId.HasValue &&
                !await _context.Exams.AnyAsync(e => e.ExamId == request.ExamId))
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest, "Invalid ExamId"));

            if (request.YearId.HasValue &&
                !await _context.Years.AnyAsync(y => y.YearId == request.YearId))
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest, "Invalid YearId"));

            // -------------------------------
            // Update scalar fields only
            // -------------------------------
            question.SubjectId = request.SubjectId ?? question.SubjectId;
            question.TopicId = request.TopicId ?? question.TopicId;
            question.ExamId = request.ExamId ?? question.ExamId;
            question.YearId = request.YearId ?? question.YearId;

            question.QuestionText = request.QuestionText ?? question.QuestionText;
            question.OptionA = request.OptionA ?? question.OptionA;
            question.OptionB = request.OptionB ?? question.OptionB;
            question.OptionC = request.OptionC ?? question.OptionC;
            question.OptionD = request.OptionD ?? question.OptionD;
            question.CorrectOption = request.CorrectOption ?? question.CorrectOption;
            question.Explanation = request.Explanation ?? question.Explanation;
            question.Source = request.Source ?? question.Source;
            question.Motivation = request.Motivation ?? question.Motivation;

            question.IsDeleted = request.IsDeleted ?? question.IsDeleted;
            question.IsOfficialAnswer = request.IsOfficialAnswer ?? question.IsOfficialAnswer;
            question.UpdatedAt = DateTime.UtcNow;

            // -------------------------------
            // Enums
            // -------------------------------
            if (!string.IsNullOrWhiteSpace(request.DifficultyLevel))
                question.DifficultyLevel = Enum.Parse<DifficultyLevel>(request.DifficultyLevel, true);

            if (!string.IsNullOrWhiteSpace(request.SourceType))
                question.SourceType = Enum.Parse<SourceType>(request.SourceType, true);

            if (!string.IsNullOrWhiteSpace(request.Nature))
                question.Nature = Enum.Parse<Nature>(request.Nature, true);

            await _context.SaveChangesAsync();

            BumpExamCandidateVersion();

            return Ok(
                ApiResponse<object>.Success(null, "Question updated successfully.")
            );
        }

        /// <summary>
        /// GET: /api/questions/recent-updates
        /// Returns the most recently updated questions.
        /// </summary>
        [HttpGet("recent-updates")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> GetRecentUpdates([FromQuery] int limit = 10)
        {
            var questions = await _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsDeleted)
                .OrderByDescending(q => q.UpdatedAt)
                .Take(limit)
                .Select(q => new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    q.IsDeleted,
                    q.IsOfficialAnswer,
                    ExamName = q.Exam != null ? q.Exam.ShortName : "Unknown",
                    SubjectName = q.Subject != null ? q.Subject.SubjectName : "Unknown",
                    TopicName = q.Topic != null ? q.Topic.TopicName : "Unknown",
                    YearName = q.Year != null ? q.Year.YearName : "Unknown"
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(new
            {
                Items = questions,
                TotalCount = questions.Count
            }));
        }


        /// <summary>
        /// GET: /api/questions/filter
        /// Filters questions based on multiple optional parameters.
        /// Orders globally by Year (latest → oldest) across exams when yearId is not provided.
        /// </summary>
        /// GET: /api/questions/filter
        [HttpGet("filter")]
        public async Task<IActionResult> Filter(
            [FromQuery] string? examIds,
            [FromQuery] int? yearId,
            [FromQuery] int? subjectId,
            [FromQuery] int? topicId,
            [FromQuery] int? questionId,
            [FromQuery] string? keyword,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] bool fetchAll = false,
            [FromQuery] bool onlyBookmarks = false)
        {
            var isAdmin = User.IsInRole("Admin");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? userId = int.TryParse(userIdClaim, out var parsedUserId)
                ? parsedUserId
                : null;

            var baseQuery = _context.Questions
                .AsNoTracking()
                .AsQueryable();

            int[] examIdsArray = Array.Empty<int>();
            if (!string.IsNullOrWhiteSpace(examIds))
            {
                examIdsArray = examIds.Split(',')
                    .Select(s => int.TryParse(s, out var id) ? id : (int?)null)
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToArray();

                if (examIdsArray.Any())
                    baseQuery = baseQuery.Where(q => q.ExamId != null && examIdsArray.Contains(q.ExamId.Value));
            }

            if (yearId.HasValue) baseQuery = baseQuery.Where(q => q.YearId == yearId);
            if (subjectId.HasValue) baseQuery = baseQuery.Where(q => q.SubjectId == subjectId);
            if (topicId.HasValue) baseQuery = baseQuery.Where(q => q.TopicId == topicId);
            if (questionId.HasValue) baseQuery = baseQuery.Where(q => q.QuestionId == questionId);

            if (string.IsNullOrWhiteSpace(keyword) &&
                !examIdsArray.Any() &&
                !yearId.HasValue &&
                !subjectId.HasValue &&
                !topicId.HasValue &&
                !questionId.HasValue &&
                !(fetchAll && isAdmin)) // Allow fetchAll for admins
            {
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest,
                    "At least one filter parameter is required."));
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var pattern = $"%{keyword}%";
                baseQuery = baseQuery.Where(q =>
                    EF.Functions.Like(q.QuestionText ?? "", pattern) ||
                    EF.Functions.Like(q.OptionA ?? "", pattern) ||
                    EF.Functions.Like(q.OptionB ?? "", pattern) ||
                    EF.Functions.Like(q.OptionC ?? "", pattern) ||
                    EF.Functions.Like(q.OptionD ?? "", pattern));
            }

            if (onlyBookmarks)
            {
                if (userId == null)
                    return Unauthorized(ApiResponse<string>.Failure(
                        ResultCode.Unauthorized,
                        "Login required"));

                var userBookmarks = _context.Bookmarks
                    .Where(b => b.UserId == userId.Value)
                    .Select(b => b.QuestionId);

                baseQuery = baseQuery.Where(q => userBookmarks.Contains(q.QuestionId));
            }

            // Total count before premium filter — free users see how many questions exist in total
            // (including premium ones they'd unlock by subscribing).
            var totalCount = await baseQuery.CountAsync();

            var query = baseQuery;

            // Non-admin users: hide questions from premium years if user has no active plan.
            // This applies to all browse modes (subject, topic, year).
            // Admin bypasses this entirely (and uses fetchAll from the admin app).
            if (!isAdmin)
            {
                bool isPremiumUser = await IsPremiumUserAsync(userId, isAdmin);
                if (!isPremiumUser)
                    query = query.Where(q => q.Year != null && q.Year.IsPremium == false);
            }

            if (yearId.HasValue && examIds != null && !subjectId.HasValue)
            {
                // Exam + year filter: return questions in the order they were added (sequential by ID)
                query = query.OrderBy(q => q.QuestionId);
            }
            else if (yearId.HasValue && !subjectId.HasValue)
            {
                query = query
                    .OrderByDescending(q => q.Year != null ? q.Year.YearOrder : int.MinValue)
                    .ThenBy(q => q.TopicId)
                    .ThenBy(q => q.QuestionId);
            }
            else if (!yearId.HasValue)
            {
                query = query
                    .OrderByDescending(q => q.YearId.HasValue)
                    .ThenByDescending(q => q.Year != null ? q.Year.YearName.Substring(0, 4) : "0000")
                    .ThenBy(q => q.ExamId)
                    .ThenByDescending(q => q.Year != null ? q.Year.YearOrder : int.MinValue)
                    .ThenBy(q => q.SubjectId)
                    .ThenBy(q => q.TopicId)
                    .ThenBy(q => q.QuestionId);
            }
            else
            {
                query = query
                    .OrderBy(q => q.ExamId)
                    .ThenBy(q => q.TopicId)
                    .ThenBy(q => q.QuestionId);
            }

            if (fetchAll && !isAdmin)
                return Forbid();

            const int MaxFetchAll = 2000;
            if (fetchAll && await query.CountAsync() > MaxFetchAll)
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest,
                    $"Query matches more than {MaxFetchAll} questions. Use pagination or narrow the filters."));

            if (!fetchAll)
                query = query.Skip((page - 1) * pageSize).Take(pageSize);

            var filteredQuestions = await query
                .Select(q => new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    q.Source,
                    q.SourceType,
                    q.DifficultyLevel,
                    q.Nature,
                    q.Motivation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    q.IsDeleted,
                    q.IsOfficialAnswer,
                    ExamName = q.Exam != null ? q.Exam.ShortName : "Unknown",
                    SubjectName = q.Subject != null ? q.Subject.SubjectName : "Unknown",
                    TopicName = q.Topic != null ? q.Topic.TopicName : "Unknown",
                    YearName = q.Year != null ? q.Year.YearName : "Unknown"
                })
                .ToListAsync();

            var questionIds = filteredQuestions.Select(q => q.QuestionId).ToList();

            var bookmarkedQuestionIds = new HashSet<int>();
            if (userId.HasValue && questionIds.Count > 0)
            {
                bookmarkedQuestionIds = (await _context.Bookmarks
                    .AsNoTracking()
                    .Where(b => b.UserId == userId.Value && questionIds.Contains(b.QuestionId))
                    .Select(b => b.QuestionId)
                    .ToListAsync())
                    .ToHashSet();
            }

            var imagesByQuestionId = await GetQuestionImagesByQuestionIdAsync(questionIds);

            var enumLabelMap = await GetEnumLabelMapAsync();

            string GetEnumLabel(string enumType, string enumName) =>
                enumLabelMap.TryGetValue((enumType, enumName), out var label)
                    ? label
                    : enumName;

            var result = filteredQuestions.Select(q =>
            {
                var imgList = imagesByQuestionId.TryGetValue(q.QuestionId, out var groupedImages)
                    ? groupedImages
                    : EmptyQuestionImageRows;

                return new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    IsBookmarked = bookmarkedQuestionIds.Contains(q.QuestionId),
                    QuestionImage = BuildImageDto(imgList, ImageType.Question),
                    OptionAImage = BuildImageDto(imgList, ImageType.OptionA),
                    OptionBImage = BuildImageDto(imgList, ImageType.OptionB),
                    OptionCImage = BuildImageDto(imgList, ImageType.OptionC),
                    OptionDImage = BuildImageDto(imgList, ImageType.OptionD),
                    AnswerImages = BuildAnswerImageDtos(imgList),
                    q.Source,
                    SourceType = q.SourceType == null ? null : new
                    {
                        Value = (int)q.SourceType,
                        Key = q.SourceType.ToString(),
                        Label = GetEnumLabel("SourceType", q.SourceType.ToString())
                    },
                    DifficultyLevel = q.DifficultyLevel == null ? null : new
                    {
                        Value = (int)q.DifficultyLevel,
                        Key = q.DifficultyLevel.ToString(),
                        Label = GetEnumLabel("DifficultyLevel", q.DifficultyLevel.ToString())
                    },
                    Nature = q.Nature == null ? null : new
                    {
                        Value = (int)q.Nature,
                        Key = q.Nature.ToString(),
                        Label = GetEnumLabel("Nature", q.Nature.ToString())
                    },
                    q.Motivation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    q.IsDeleted,
                    q.IsOfficialAnswer,
                    q.ExamName,
                    q.SubjectName,
                    q.TopicName,
                    q.YearName
                };
            }).ToList();

            return Ok(ApiResponse<object>.Paginated(result, totalCount, page, pageSize));
        }

        [HttpGet("exam")]
        public async Task<IActionResult> GetExamQuestions(
            [FromQuery] int? yearId,
            [FromQuery] int? subjectId,
            [FromQuery] int? topicId,
            [FromQuery] int[]? yearIds,
            [FromQuery] int? questionCount)
        {
            if (!yearId.HasValue && !subjectId.HasValue && !topicId.HasValue)
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest,
                    "At least one of yearId, subjectId, or topicId is required."
                ));

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.Unauthorized,
                    "User not authorized"
                ));

            var query = _context.Questions
                .AsNoTracking()
                .Where(q => !q.IsDeleted)
                .AsQueryable();

            var isAdmin = User.IsInRole("Admin");

            bool isPremiumUser = await IsPremiumUserAsync(userId, isAdmin);

            if (!isPremiumUser)
                query = query.Where(q => q.Year != null && q.Year.IsPremium == false);

            int[]? shuffledQuestionIds = null;

            if (yearId.HasValue)
            {
                var userExists = await _context.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.UserId == userId);

                if (!userExists)
                    return Unauthorized(ApiResponse<string>.Failure(
                        ResultCode.Unauthorized,
                        "User not found"
                    ));

                query = query
                    .Where(q => q.YearId == yearId.Value)
                    .OrderBy(q => q.QuestionId);
            }
            else
            {
                var selectedExamIds = await GetSelectedExamIdsAsync(userId);
                if (selectedExamIds == null)
                    return Unauthorized(ApiResponse<string>.Failure(
                        ResultCode.Unauthorized,
                        "User not found"
                    ));

                if (!selectedExamIds.Any())
                    return BadRequest(ApiResponse<string>.Failure(
                        ResultCode.BadRequest,
                        "User has no selected exams."
                    ));

                query = query.Where(q =>
                    q.ExamId != null &&
                    selectedExamIds.Contains(q.ExamId.Value)
                );

                if (subjectId.HasValue || topicId.HasValue)
                {
                    if (!questionCount.HasValue)
                        return BadRequest(ApiResponse<string>.Failure(
                            ResultCode.ValidationError,
                            "Question count is required for subject/topic exams."
                        ));

                    if (subjectId.HasValue)
                        query = query.Where(q => q.SubjectId == subjectId.Value);

                    if (topicId.HasValue)
                        query = query.Where(q => q.TopicId == topicId.Value);
                }

                if (yearIds != null && yearIds.Any())
                    query = query.Where(q =>
                        q.YearId.HasValue &&
                        yearIds.Contains(q.YearId.Value)
                    );

                if (questionCount.HasValue)
                {
                    var allowedCounts = new[] { 5, 10, 25, 50, 100 };
                    if (!allowedCounts.Contains(questionCount.Value))
                        return BadRequest(ApiResponse<string>.Failure(
                            ResultCode.ValidationError,
                            "Invalid question count. Allowed values: 5, 10, 25, 50, 100"
                        ));

                    var selectedExamIdsCacheKey = string.Join(",", selectedExamIds.OrderBy(id => id));
                    var yearIdsCacheKey = yearIds == null || yearIds.Length == 0
                        ? "-"
                        : string.Join(",", yearIds.OrderBy(id => id));
                    var version = _cache.Get<int>(CacheKeys.ExamCandidateVersion);
                    var candidateIdsCacheKey = CacheKeys.ExamCandidateIds(
                        userId, isPremiumUser, subjectId, topicId, yearIdsCacheKey, selectedExamIdsCacheKey, version);

                    List<int>? cachedCandidateQuestionIds;
                    if (!_cache.TryGetValue(candidateIdsCacheKey, out cachedCandidateQuestionIds))
                    {
                        cachedCandidateQuestionIds = await query
                            .Select(q => q.QuestionId)
                            .ToListAsync();

                        _cache.Set(
                            candidateIdsCacheKey,
                            cachedCandidateQuestionIds,
                            new MemoryCacheEntryOptions
                            {
                                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
                                Size = 1
                            });
                    }

                    var candidateQuestionIds = cachedCandidateQuestionIds.ToList();

                    var totalAvailable = candidateQuestionIds.Count;

                    if (totalAvailable < questionCount.Value)
                        return BadRequest(ApiResponse<string>.Failure(
                            ResultCode.NotEnoughQuestions,
                            $"Only {totalAvailable} questions available for the selected criteria."
                        ));

                    ShuffleInPlace(candidateQuestionIds);
                    shuffledQuestionIds = candidateQuestionIds
                        .Take(questionCount.Value)
                        .ToArray();

                    query = _context.Questions
                        .AsNoTracking()
                        .Where(q => shuffledQuestionIds.Contains(q.QuestionId));
                }
            }

            var questions = await query
                .Select(q => new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    q.SourceType,
                    q.DifficultyLevel,
                    q.Nature,
                    q.Motivation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    ExamName = q.Exam != null ? q.Exam.ShortName : "Unknown",
                    SubjectName = q.Subject != null ? q.Subject.SubjectName : "Unknown",
                    TopicName = q.Topic != null ? q.Topic.TopicName : "Unknown",
                    YearName = q.Year != null ? q.Year.YearName : "Unknown",
                    q.IsOfficialAnswer
                })
                .ToListAsync();

            if (shuffledQuestionIds != null)
            {
                var orderMap = shuffledQuestionIds
                    .Select((id, index) => new { id, index })
                    .ToDictionary(x => x.id, x => x.index);

                questions = questions
                    .OrderBy(q => orderMap.TryGetValue(q.QuestionId, out var index) ? index : int.MaxValue)
                    .ToList();
            }

            var questionIds = questions.Select(q => q.QuestionId).ToList();

            var bookmarkedQuestionIds = questionIds.Count == 0
                ? new HashSet<int>()
                : (await _context.Bookmarks
                    .AsNoTracking()
                    .Where(b => b.UserId == userId && questionIds.Contains(b.QuestionId))
                    .Select(b => b.QuestionId)
                    .ToListAsync())
                    .ToHashSet();

            var imagesByQuestionId = await GetQuestionImagesByQuestionIdAsync(questionIds);

            var result = questions.Select(q =>
            {
                var imgList = imagesByQuestionId.TryGetValue(q.QuestionId, out var groupedImages)
                    ? groupedImages
                    : EmptyQuestionImageRows;

                return new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    QuestionImage = BuildImageDto(imgList, ImageType.Question),
                    OptionAImage = BuildImageDto(imgList, ImageType.OptionA),
                    OptionBImage = BuildImageDto(imgList, ImageType.OptionB),
                    OptionCImage = BuildImageDto(imgList, ImageType.OptionC),
                    OptionDImage = BuildImageDto(imgList, ImageType.OptionD),
                    AnswerImages = BuildAnswerImageDtos(imgList),
                    q.SourceType,
                    q.DifficultyLevel,
                    q.Nature,
                    q.Motivation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    q.ExamName,
                    q.SubjectName,
                    q.TopicName,
                    q.YearName,
                    q.IsOfficialAnswer,
                    IsBookmarked = bookmarkedQuestionIds.Contains(q.QuestionId)
                };
            }).ToList();

            return Ok(ApiResponse<object>.Success(result));
        }



        /// <summary>
        /// POST: /api/questions
        /// Create a new question. Admin only.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Create([FromBody] CreateQuestionRequest request)
        {
            // Validate enums
            QuestionEnums.SourceType? sourceType = null;
            QuestionEnums.DifficultyLevel? difficultyLevel = null;
            QuestionEnums.Nature? nature = null;

            if (!string.IsNullOrWhiteSpace(request.SourceType) &&
                !Enum.TryParse(request.SourceType, true, out QuestionEnums.SourceType s))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Invalid SourceType."));
            else sourceType = Enum.TryParse(request.SourceType, true, out s) ? s : null;

            if (!string.IsNullOrWhiteSpace(request.DifficultyLevel) &&
                !Enum.TryParse(request.DifficultyLevel, true, out QuestionEnums.DifficultyLevel d))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Invalid DifficultyLevel."));
            else difficultyLevel = Enum.TryParse(request.DifficultyLevel, true, out d) ? d : null;

            if (!string.IsNullOrWhiteSpace(request.Nature) &&
                !Enum.TryParse(request.Nature, true, out QuestionEnums.Nature n))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Invalid Nature."));
            else nature = Enum.TryParse(request.Nature, true, out n) ? n : null;

            var question = new Question
            {
                ExamId = request.ExamId,
                SubjectId = request.SubjectId,
                TopicId = request.TopicId,
                YearId = request.YearId,
                QuestionText = request.QuestionText,
                OptionA = request.OptionA,
                OptionB = request.OptionB,
                OptionC = request.OptionC,
                OptionD = request.OptionD,
                CorrectOption = request.CorrectOption,
                Explanation = request.Explanation,
                Source = request.Source,
                SourceType = sourceType,
                DifficultyLevel = difficultyLevel,
                Nature = nature,
                Motivation = request.Motivation,
                UpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = request.IsDeleted,
                IsOfficialAnswer = request.IsOfficialAnswer
            };

            _context.Questions.Add(question);
            await _context.SaveChangesAsync();

            BumpExamCandidateVersion();

            // Return newly created question with empty image fields
            var result = new
            {
                question.QuestionId,
                QuestionImage = (object?)null,
                OptionAImage = (object?)null,
                OptionBImage = (object?)null,
                OptionCImage = (object?)null,
                OptionDImage = (object?)null,
                AnswerImages = Array.Empty<object>()
            };

            return Ok(ApiResponse<object>.Success(result, "Question created successfully."));
        }

        /// <summary>
        /// DELETE: /api/questions/{id}
        /// Soft delete a question. Admin only.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var question = await _context.Questions.FindAsync(id);
            if (question == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found."));

            if (question.IsDeleted)
                return Conflict(ApiResponse<string>.Failure(ResultCode.Conflict, "Question is already deleted."));

            question.IsDeleted = true;
            await _context.SaveChangesAsync();

            BumpExamCandidateVersion();

            return Ok(ApiResponse<string>.Success("Question soft-deleted successfully."));
        }

        /// <summary>
        /// DELETE: /api/questions/{id}/permanent
        /// Permanently deletes a question and all associated images from storage. Admin only.
        /// </summary>
        [HttpDelete("{id}/permanent")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> PermanentDelete(int id)
        {
            var question = await _context.Questions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(q => q.QuestionId == id);

            if (question == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found."));

            // Load and delete associated images from storage
            var imageLinks = await _context.QuestionAnswerImages
                .Include(qa => qa.Image)
                .Where(qa => qa.QuestionId == id)
                .ToListAsync();

            foreach (var link in imageLinks)
            {
                try { await _storage.DeleteAsync(link.Image.BucketKey); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete image {Key} from storage.", link.Image.BucketKey); }

                _context.Images.Remove(link.Image);
            }

            _context.QuestionAnswerImages.RemoveRange(imageLinks);
            _context.Questions.Remove(question);
            await _context.SaveChangesAsync();

            BumpExamCandidateVersion();

            return Ok(ApiResponse<string>.Success("Question permanently deleted."));
        }

        /// <summary>
        /// POST: /api/questions/{id}/restore
        /// Restore a soft deleted question. Admin only.
        /// </summary>
        [HttpPost("{id}/restore")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Restore(int id)
        {
            var question = await _context.Questions
                .IgnoreQueryFilters() // Include deleted questions
                .FirstOrDefaultAsync(q => q.QuestionId == id);

            if (question == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found."));

            if (!question.IsDeleted)
                return Conflict(ApiResponse<string>.Failure(ResultCode.Conflict, "Question is not deleted."));

            question.IsDeleted = false;
            await _context.SaveChangesAsync();

            BumpExamCandidateVersion();

            return Ok(ApiResponse<string>.Success("Question restored successfully."));
        }

        // 🔍 GET: /api/questions/deleted
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpGet("deleted")]
        public async Task<IActionResult> GetDeletedQuestions()
        {
            var deletedQuestions = await _context.Questions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(q => q.IsDeleted)
                .OrderByDescending(q => q.UpdatedAt)
                .Select(q => new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    q.Source,
                    SourceType = q.SourceType != null ? q.SourceType.ToString() : null,
                    DifficultyLevel = q.DifficultyLevel != null ? q.DifficultyLevel.ToString() : null,
                    Nature = q.Nature != null ? q.Nature.ToString() : null,
                    q.Motivation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    ExamName = q.Exam != null ? q.Exam.ShortName : "Unknown",
                    SubjectName = q.Subject != null ? q.Subject.SubjectName : "Unknown",
                    TopicName = q.Topic != null ? q.Topic.TopicName : "Unknown",
                    YearName = q.Year != null ? q.Year.YearName : "Unknown",
                    q.IsDeleted,
                    q.IsOfficialAnswer
                })
                .ToListAsync();

            var questionIds = deletedQuestions.Select(q => q.QuestionId).ToList();

            var images = questionIds.Count == 0
                ? new List<QuestionAnswerImage>()
                : await _context.QuestionAnswerImages
                    .AsNoTracking()
                    .Include(qa => qa.Image)
                    .Where(qa => questionIds.Contains(qa.QuestionId))
                    .ToListAsync();

            var imagesByQuestionId = images
                .GroupBy(i => i.QuestionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = deletedQuestions.Select(q =>
            {
                var imgList = imagesByQuestionId.TryGetValue(q.QuestionId, out var groupedImages)
                    ? groupedImages
                    : new List<QuestionAnswerImage>();

                return new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    QuestionImage = imgList.FirstOrDefault(i => i.Image.ImageType == ImageType.Question) is var qi && qi != null
                        ? new
                        {
                            Url = _mediaUrl.Build(qi.Image.BucketKey),
                            ImageId = qi.Image.ImageId
                        }
                        : null,
                    OptionAImage = imgList.FirstOrDefault(i => i.Image.ImageType == ImageType.OptionA) is var oa && oa != null
                        ? new
                        {
                            Url = _mediaUrl.Build(oa.Image.BucketKey),
                            ImageId = oa.Image.ImageId
                        }
                        : null,
                    OptionBImage = imgList.FirstOrDefault(i => i.Image.ImageType == ImageType.OptionB) is var ob && ob != null
                        ? new
                        {
                            Url = _mediaUrl.Build(ob.Image.BucketKey),
                            ImageId = ob.Image.ImageId
                        }
                        : null,
                    OptionCImage = imgList.FirstOrDefault(i => i.Image.ImageType == ImageType.OptionC) is var oc && oc != null
                        ? new
                        {
                            Url = _mediaUrl.Build(oc.Image.BucketKey),
                            ImageId = oc.Image.ImageId
                        }
                        : null,
                    OptionDImage = imgList.FirstOrDefault(i => i.Image.ImageType == ImageType.OptionD) is var od && od != null
                        ? new
                        {
                            Url = _mediaUrl.Build(od.Image.BucketKey),
                            ImageId = od.Image.ImageId
                        }
                        : null,
                    AnswerImages = imgList
                        .Where(i => i.Image.ImageType == ImageType.Answer)
                        .Select(i => new
                        {
                            Url = _mediaUrl.Build(i.Image.BucketKey),
                            ImageId = i.Image.ImageId
                        })
                        .ToList(),
                    q.Source,
                    q.SourceType,
                    q.DifficultyLevel,
                    q.Nature,
                    q.Motivation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    q.ExamName,
                    q.SubjectName,
                    q.TopicName,
                    q.YearName,
                    q.IsDeleted,
                    q.IsOfficialAnswer
                };
            }).ToList();

            return Ok(ApiResponse<object>.Success(result));
        }

        // 📚 GET: /api/questions/by-ids
        [HttpGet("by-ids")]
        [Authorize]
        public async Task<IActionResult> GetQuestionsByIds([FromQuery] List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Please provide at least one question ID."));

            var questions = await _context.Questions
                .AsNoTracking()
                .Where(q => ids.Contains(q.QuestionId))
                .Select(q => new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    q.Source,
                    SourceType = q.SourceType != null ? q.SourceType.ToString() : null,
                    DifficultyLevel = q.DifficultyLevel != null ? q.DifficultyLevel.ToString() : null,
                    Nature = q.Nature != null ? q.Nature.ToString() : null,
                    q.Motivation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    ExamName = q.Exam != null ? q.Exam.ShortName : "Unknown",
                    SubjectName = q.Subject != null ? q.Subject.SubjectName : "Unknown",
                    TopicName = q.Topic != null ? q.Topic.TopicName : "Unknown",
                    YearName = q.Year != null ? q.Year.YearName : "Unknown",
                    q.IsDeleted,
                    q.IsOfficialAnswer
                })
                .ToListAsync();

            var questionIds = questions.Select(q => q.QuestionId).ToList();

            var images = questionIds.Count == 0
                ? new List<QuestionAnswerImage>()
                : await _context.QuestionAnswerImages
                    .AsNoTracking()
                    .Include(qa => qa.Image)
                    .Where(qa => questionIds.Contains(qa.QuestionId))
                    .ToListAsync();

            var imagesByQuestionId = images
                .GroupBy(i => i.QuestionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = questions.Select(q =>
            {
                var imgList = imagesByQuestionId.TryGetValue(q.QuestionId, out var groupedImages)
                    ? groupedImages
                    : new List<QuestionAnswerImage>();

                return new
                {
                    q.QuestionId,
                    q.QuestionText,
                    q.OptionA,
                    q.OptionB,
                    q.OptionC,
                    q.OptionD,
                    q.CorrectOption,
                    q.Explanation,
                    AnswerImages = imgList
                        .Select(qa => new
                        {
                            Url = _mediaUrl.Build(qa.Image.BucketKey),
                            ImageId = qa.Image.ImageId
                        })
                        .ToList(),
                    q.Source,
                    q.SourceType,
                    q.DifficultyLevel,
                    q.Nature,
                    q.Motivation,
                    q.UpdatedAt,
                    q.CreatedAt,
                    q.ExamId,
                    q.SubjectId,
                    q.TopicId,
                    q.YearId,
                    q.ExamName,
                    q.SubjectName,
                    q.TopicName,
                    q.YearName,
                    q.IsDeleted,
                    q.IsOfficialAnswer
                };
            }).ToList();

            return Ok(ApiResponse<object>.Success(result));
        }


        // 📌 GET: /api/questions/labels
        [HttpGet("labels")]
        public async Task<IActionResult> GetLabels()
        {
            var allLabels = await _context.EnumLabels
                .AsNoTracking()
                .Select(e => new
                {
                    e.EnumType,
                    e.EnumName,
                    e.DisplayLabel
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(allLabels));
        }


        [Authorize]
        [HttpPost("report/{questionId}")]
        public async Task<IActionResult> ReportQuestion(int questionId, [FromBody] ReportQuestionRequest request)
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "User authentication failed."));
            }

            // At least one reason must be selected
            if (!(request.WrongAnswer || request.WrongExplanation || request.WrongOptions ||
                  request.QuestionFormatting || request.DuplicateQuestion || request.Other))
            {
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Select at least one reason to report."));
            }

            // Optional: Check question existence
            var exists = await _context.Questions.AnyAsync(q => q.QuestionId == questionId);
            if (!exists)
            {
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found."));
            }

            var report = new QuestionReport
            {
                UserId = userId,
                QuestionId = questionId,
                WrongAnswer = request.WrongAnswer,
                WrongExplanation = request.WrongExplanation,
                WrongOptions = request.WrongOptions,
                QuestionFormatting = request.QuestionFormatting,
                DuplicateQuestion = request.DuplicateQuestion,
                Other = request.Other,
                OtherDetails = request.OtherDetails,
                CreatedAt = DateTime.UtcNow
            };

            _context.QuestionReports.Add(report);
            await _context.SaveChangesAsync();

            // Push live count to all connected admins
            var unresolvedCount = await _context.QuestionReports.CountAsync(r => !r.IsResolved);
            await _adminHub.Clients.All.SendAsync("NewReport", unresolvedCount);

            var user = await _context.Users.FindAsync(userId);

            if (user != null && !string.IsNullOrEmpty(user.UserEmail))
            {
                try
                {
                    await _emailService.SendReportReceivedEmail(
                        user.UserEmail,
                        user.Username,
                        questionId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send report received email.");
                }
            }

            return Ok(ApiResponse<string>.Success("Report submitted successfully."));
        }


        public class BulkAiRequest
        {
            public int BatchSize { get; set; } = 10;
            public string? CustomPromptTemplate { get; set; }
        }

        public class AiQuestionData
        {
            public string? QuestionText { get; set; }
            public string? OptionA { get; set; }
            public string? OptionB { get; set; }
            public string? OptionC { get; set; }
            public string? OptionD { get; set; }
            public string? CorrectOption { get; set; }
        }

        public class SingleAiRequest
        {
            public AiQuestionData QuestionData { get; set; } = new();
            public string? CustomPromptTemplate { get; set; }
        }

        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("bulk-generate-explanations")]
        public async Task<IActionResult> BulkGenerateExplanations([FromServices] pqy_server.Services.AiService.AiProviderFactory aiFactory, [FromBody] BulkAiRequest request)
        {
            var provider = aiFactory.GetActiveProvider();
            var systemPrompt = !string.IsNullOrWhiteSpace(request.CustomPromptTemplate) 
                ? request.CustomPromptTemplate 
                : aiFactory.GetSystemPrompt();
            var userPromptTemplate = aiFactory.GetUserPromptTemplate();

            int batchSize = request.BatchSize > 0 ? request.BatchSize : 5;

            var questions = await _context.Questions
                .IgnoreQueryFilters()
                .Where(q => string.IsNullOrWhiteSpace(q.Explanation)
                    && q.QuestionText != "."
                    && q.OptionA != "."
                    && q.OptionB != "."
                    && q.OptionC != "."
                    && q.OptionD != ".")
                .OrderBy(q => q.QuestionId)
                .Take(batchSize)
                .ToListAsync();

            if (!questions.Any())
            {
                return Ok(ApiResponse<string>.Success("No questions found without explanations."));
            }

            int successCount = 0;
            var updatedIds = new List<int>();
            var errors = new List<string>();

            // Parallel execution of AI requests
            var tasks = questions.Select(async q => 
            {
                try
                {
                    var explanation = await provider.GenerateExplanationAsync(q, systemPrompt, userPromptTemplate);
                    return new { QuestionId = q.QuestionId, Explanation = explanation, Success = true, Error = (string?)null };
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Parallel AI generation failed for Question {Id}", q.QuestionId);
                    return new { QuestionId = q.QuestionId, Explanation = (string?)null, Success = false, Error = ex.Message };
                }
            });

            var results = await Task.WhenAll(tasks);

            // Sequential DB update for safety
            foreach (var res in results)
            {
                var q = questions.First(x => x.QuestionId == res.QuestionId);
                if (res.Success)
                {
                    q.Explanation = res.Explanation;
                    q.UpdatedAt = DateTime.UtcNow;
                    successCount++;
                    updatedIds.Add(q.QuestionId);
                }
                else
                {
                    errors.Add($"Question {res.QuestionId}: {res.Error}");
                }
            }

            if (successCount > 0)
            {
                await _context.SaveChangesAsync();
                await _adminHub.Clients.All.SendAsync("ReceiveProgress", $"AI Generated {successCount} explanations.");
            }

            return Ok(ApiResponse<object>.Success(new { 
                Message = $"Processed {questions.Count} questions.",
                UpdatedQuestionIds = updatedIds,
                Errors = errors
            }));
        }


        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("generate-explanation")]
        public async Task<IActionResult> GenerateExplanation([FromServices] pqy_server.Services.AiService.AiProviderFactory aiFactory, [FromBody] SingleAiRequest request)
        {
            var provider = aiFactory.GetActiveProvider();
            var systemPrompt = !string.IsNullOrWhiteSpace(request.CustomPromptTemplate) 
                ? request.CustomPromptTemplate 
                : aiFactory.GetSystemPrompt();
            var userPromptTemplate = aiFactory.GetUserPromptTemplate();

            try
            {
                // Create a dummy Question object for the provider
                var dummyQuestion = new pqy_server.Models.Questions.Question
                {
                    QuestionText = request.QuestionData.QuestionText,
                    OptionA = request.QuestionData.OptionA,
                    OptionB = request.QuestionData.OptionB,
                    OptionC = request.QuestionData.OptionC,
                    OptionD = request.QuestionData.OptionD,
                    CorrectOption = request.QuestionData.CorrectOption
                };

                var explanation = await provider.GenerateExplanationAsync(dummyQuestion, systemPrompt, userPromptTemplate);
                return Ok(ApiResponse<string>.Success(explanation, "Explanation generated successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate explanation for single question");
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, ex.Message));
            }
        }

        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("create-gemini-batch")]
        public async Task<IActionResult> CreateGeminiBatch([FromServices] pqy_server.Services.AiService.AiProviderFactory aiFactory, [FromQuery] int take = 1000)
        {
            var provider = aiFactory.GetActiveProvider();
            if (provider.ProviderName != "Gemini")
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Batch API is only implemented for Gemini."));

            var questions = await _context.Questions
                .IgnoreQueryFilters()
                .Where(q => string.IsNullOrWhiteSpace(q.Explanation)
                    && q.QuestionText != "."
                    && q.OptionA != "."
                    && q.OptionB != "."
                    && q.OptionC != "."
                    && q.OptionD != ".")
                .OrderBy(q => q.QuestionId)
                .Take(take)
                .ToListAsync();

            if (!questions.Any())
                return Ok(ApiResponse<string>.Success("No questions found needing explanations."));

            try
            {
                var systemPrompt = aiFactory.GetSystemPrompt();
                var userPromptTemplate = aiFactory.GetUserPromptTemplate();
                var jobId = await provider.SubmitBatchJobAsync(questions, systemPrompt, userPromptTemplate);
                return Ok(ApiResponse<string>.Success(jobId, "Gemini Batch Job submitted successfully."));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Gemini batch job");
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, ex.Message));
            }
        }

        [Authorize(Roles = RoleConstant.Admin)]
        [HttpGet("gemini-batch-status/{*jobId}")]
        public async Task<IActionResult> GetGeminiBatchStatus([FromServices] pqy_server.Services.AiService.AiProviderFactory aiFactory, string jobId)
        {
            var provider = aiFactory.GetActiveProvider();
            try
            {
                var (state, outputFile) = await provider.GetBatchJobStatusAsync(jobId);
                return Ok(ApiResponse<object>.Success(new { state, outputFile }));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, ex.Message));
            }
        }

        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("sync-gemini-batch")]
        public async Task<IActionResult> SyncGeminiBatch([FromServices] pqy_server.Services.AiService.AiProviderFactory aiFactory, [FromQuery] string outputFileUri)
        {
            var provider = aiFactory.GetActiveProvider();
            try
            {
                var results = await provider.DownloadBatchResultsAsync(outputFileUri);
                int count = 0;
                foreach (var (qId, explanation) in results)
                {
                    var q = await _context.Questions.FindAsync(qId);
                    if (q != null && string.IsNullOrWhiteSpace(q.Explanation))
                    {
                        q.Explanation = explanation;
                        q.UpdatedAt = DateTime.UtcNow;
                        count++;
                    }
                }
                await _context.SaveChangesAsync();
                return Ok(ApiResponse<string>.Success($"Successfully synced {count} explanations from batch."));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, ex.Message));
            }
        }


        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("bulk-upload")]
        public async Task<IActionResult> BulkUpload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest,
                    "Please upload a valid Excel (.xlsx) file."
                ));

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            stream.Position = 0;

            using var package = new ExcelPackage(stream);
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest,
                    "No worksheet found in uploaded Excel file."
                ));

            if (worksheet.Dimension == null)
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.BadRequest,
                    "The worksheet appears to be empty."
                ));

            int rowCount = worksheet.Dimension.Rows;
            int updated = 0, inserted = 0;

            // ── Phase 0: parse all rows first, then bulk-fetch every referenced ID ──

            // Parsed row data (no DB calls yet)
            var rows = new List<(int row, int questionId, CreateQuestionRequest dto)>(rowCount - 1);

            for (int row = 2; row <= rowCount; row++)
            {
                int.TryParse(worksheet.Cells[row, 1].Text?.Trim(), out var questionId);

                var dto = new CreateQuestionRequest
                {
                    ExamId = int.TryParse(worksheet.Cells[row, 2].Text, out var examId) ? examId : null,
                    SubjectId = int.TryParse(worksheet.Cells[row, 3].Text, out var subjectId) ? subjectId : null,
                    TopicId = int.TryParse(worksheet.Cells[row, 4].Text, out var topicId) ? topicId : null,
                    YearId = int.TryParse(worksheet.Cells[row, 5].Text, out var yearId) ? yearId : null,

                    QuestionText = worksheet.Cells[row, 6].Text?.Trim(),
                    OptionA = worksheet.Cells[row, 7].Text?.Trim(),
                    OptionB = worksheet.Cells[row, 8].Text?.Trim(),
                    OptionC = worksheet.Cells[row, 9].Text?.Trim(),
                    OptionD = worksheet.Cells[row, 10].Text?.Trim(),
                    CorrectOption = worksheet.Cells[row, 11].Text?.Trim(),
                    Explanation = worksheet.Cells[row, 12].Text?.Trim(),

                    Source = worksheet.Cells[row, 13].Text?.Trim(),
                    SourceType = worksheet.Cells[row, 14].Text?.Trim(),
                    DifficultyLevel = worksheet.Cells[row, 15].Text?.Trim(),
                    Nature = worksheet.Cells[row, 16].Text?.Trim(),
                    Motivation = worksheet.Cells[row, 17].Text?.Trim(),
                    IsOfficialAnswer = bool.TryParse(
                        worksheet.Cells[row, 18].Text?.Trim(),
                        out var isOfficial
                    ) && isOfficial
                };

                rows.Add((row, questionId, dto));
            }

            // Collect all unique IDs referenced across the sheet
            var allSubjectIds   = rows.Where(r => r.dto.SubjectId.HasValue).Select(r => r.dto.SubjectId!.Value).ToHashSet();
            var allTopicIds     = rows.Where(r => r.dto.TopicId.HasValue).Select(r => r.dto.TopicId!.Value).ToHashSet();
            var allExamIds      = rows.Where(r => r.dto.ExamId.HasValue).Select(r => r.dto.ExamId!.Value).ToHashSet();
            var allYearIds      = rows.Where(r => r.dto.YearId.HasValue).Select(r => r.dto.YearId!.Value).ToHashSet();
            var allQuestionIds  = rows.Where(r => r.questionId > 0).Select(r => r.questionId).ToHashSet();

            // Bulk fetch all referenced entities in parallel — one DB round-trip each
            var validSubjectIds = await _context.Subjects
                .Where(s => allSubjectIds.Contains(s.SubjectId))
                .Select(s => s.SubjectId)
                .ToHashSetAsync();

            var validTopicIds = allTopicIds.Count > 0
                ? await _context.Topics
                    .Where(t => allTopicIds.Contains(t.TopicId))
                    .Select(t => t.TopicId)
                    .ToHashSetAsync()
                : new HashSet<int>();

            var validExamIds = allExamIds.Count > 0
                ? await _context.Exams
                    .Where(e => allExamIds.Contains(e.ExamId))
                    .Select(e => e.ExamId)
                    .ToHashSetAsync()
                : new HashSet<int>();

            var validYearIds = allYearIds.Count > 0
                ? await _context.Years
                    .Where(y => allYearIds.Contains(y.YearId))
                    .Select(y => y.YearId)
                    .ToHashSetAsync()
                : new HashSet<int>();

            var existingQuestions = allQuestionIds.Count > 0
                ? await _context.Questions
                    .Where(q => allQuestionIds.Contains(q.QuestionId))
                    .ToDictionaryAsync(q => q.QuestionId)
                : new Dictionary<int, Question>();

            // ── Phase 1: validate + stage all changes (no DB calls) ──

            foreach (var (row, questionId, dto) in rows)
            {
                // REQUIRED FIELD VALIDATION
                if (string.IsNullOrWhiteSpace(dto.QuestionText) ||
                    string.IsNullOrWhiteSpace(dto.OptionA) ||
                    string.IsNullOrWhiteSpace(dto.OptionB) ||
                    string.IsNullOrWhiteSpace(dto.OptionC) ||
                    string.IsNullOrWhiteSpace(dto.OptionD) ||
                    string.IsNullOrWhiteSpace(dto.CorrectOption) ||
                    !dto.SubjectId.HasValue)
                {
                    return BadRequest(ApiResponse<string>.Failure(
                        ResultCode.BadRequest,
                        $"Row {row}: Missing required fields."
                    ));
                }

                // FK VALIDATION (in-memory)
                if (!validSubjectIds.Contains(dto.SubjectId!.Value))
                    return BadRequest($"Row {row}: Invalid SubjectId.");

                if (dto.TopicId.HasValue && !validTopicIds.Contains(dto.TopicId.Value))
                    return BadRequest($"Row {row}: Invalid TopicId.");

                if (dto.ExamId.HasValue && !validExamIds.Contains(dto.ExamId.Value))
                    return BadRequest($"Row {row}: Invalid ExamId.");

                if (dto.YearId.HasValue && !validYearIds.Contains(dto.YearId.Value))
                    return BadRequest($"Row {row}: Invalid YearId.");

                // ENUM PARSING
                QuestionEnums.SourceType? sourceType = null;
                if (!string.IsNullOrWhiteSpace(dto.SourceType))
                {
                    if (!Enum.TryParse(dto.SourceType, true, out QuestionEnums.SourceType s))
                        return BadRequest($"Row {row}: Invalid SourceType.");
                    sourceType = s;
                }

                QuestionEnums.DifficultyLevel? difficultyLevel = null;
                if (!string.IsNullOrWhiteSpace(dto.DifficultyLevel))
                {
                    if (!Enum.TryParse(dto.DifficultyLevel, true, out QuestionEnums.DifficultyLevel d))
                        return BadRequest($"Row {row}: Invalid DifficultyLevel.");
                    difficultyLevel = d;
                }

                QuestionEnums.Nature? nature = null;
                if (!string.IsNullOrWhiteSpace(dto.Nature))
                {
                    if (!Enum.TryParse(dto.Nature, true, out QuestionEnums.Nature n))
                        return BadRequest($"Row {row}: Invalid Nature.");
                    nature = n;
                }

                // UPDATE
                if (questionId > 0)
                {
                    if (!existingQuestions.TryGetValue(questionId, out var existing))
                        return BadRequest($"Row {row}: QuestionId {questionId} not found.");

                    if (existing.IsDeleted)
                        return BadRequest($"Row {row}: QuestionId {questionId} is deleted.");

                    existing.ExamId = dto.ExamId;
                    existing.SubjectId = dto.SubjectId.Value;
                    existing.TopicId = dto.TopicId;
                    existing.YearId = dto.YearId;
                    existing.QuestionText = dto.QuestionText;
                    existing.OptionA = dto.OptionA;
                    existing.OptionB = dto.OptionB;
                    existing.OptionC = dto.OptionC;
                    existing.OptionD = dto.OptionD;
                    existing.CorrectOption = dto.CorrectOption;
                    existing.Explanation = dto.Explanation;
                    existing.Source = dto.Source;
                    existing.SourceType = sourceType;
                    existing.DifficultyLevel = difficultyLevel;
                    existing.Nature = nature;
                    existing.Motivation = dto.Motivation;
                    existing.IsOfficialAnswer = dto.IsOfficialAnswer;
                    existing.UpdatedAt = DateTime.UtcNow;

                    updated++;
                }
                // INSERT
                else
                {
                    _context.Questions.Add(new Question
                    {
                        ExamId = dto.ExamId,
                        SubjectId = dto.SubjectId.Value,
                        TopicId = dto.TopicId,
                        YearId = dto.YearId,
                        QuestionText = dto.QuestionText,
                        OptionA = dto.OptionA,
                        OptionB = dto.OptionB,
                        OptionC = dto.OptionC,
                        OptionD = dto.OptionD,
                        CorrectOption = dto.CorrectOption,
                        Explanation = dto.Explanation,
                        Source = dto.Source,
                        SourceType = sourceType,
                        DifficultyLevel = difficultyLevel,
                        Nature = nature,
                        Motivation = dto.Motivation,
                        IsOfficialAnswer = dto.IsOfficialAnswer,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });

                    inserted++;
                }
            }

            // Phase 2: flush all staged changes in one atomic SaveChangesAsync,
            // wrapped in the execution strategy so retries work correctly.
            await _context.Database.CreateExecutionStrategy()
                .ExecuteAsync(() => _context.SaveChangesAsync());

            BumpExamCandidateVersion();

            return Ok(ApiResponse<object>.Success(new
            {
                message = $"{updated} updated, {inserted} inserted successfully.",
                updated,
                inserted
            }));
        }


        [Authorize(Roles = RoleConstant.Admin)]
        [HttpGet("export")]
        public async Task<IActionResult> ExportQuestionsToExcel(
            [FromQuery] int? examId,
            [FromQuery] int? yearId,
            [FromQuery] int? subjectId,
            [FromQuery] int? topicId
        )
        {
            var query = _context.Questions.AsQueryable();

            if (examId.HasValue)
                query = query.Where(q => q.ExamId == examId);

            if (yearId.HasValue)
                query = query.Where(q => q.YearId == yearId);

            if (subjectId.HasValue)
                query = query.Where(q => q.SubjectId == subjectId);

            if (topicId.HasValue)
                query = query.Where(q => q.TopicId == topicId);

            var questions = await query.AsNoTracking().ToListAsync();

            if (!questions.Any())
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.NotFound,
                    "No questions found for export."
                ));

            // -------- Fetch names for filename --------

            var examName = examId.HasValue
                ? await _context.Exams
                    .Where(e => e.ExamId == examId)
                    .Select(e => e.ExamName)
                    .FirstOrDefaultAsync()
                : null;

            var yearName = yearId.HasValue
                ? await _context.Years
                    .Where(y => y.YearId == yearId)
                    .Select(y => y.YearName)
                    .FirstOrDefaultAsync()
                : null;

            var subjectName = subjectId.HasValue
                ? await _context.Subjects
                    .Where(s => s.SubjectId == subjectId)
                    .Select(s => s.SubjectName)
                    .FirstOrDefaultAsync()
                : null;

            var topicName = topicId.HasValue
                ? await _context.Topics
                    .Where(t => t.TopicId == topicId)
                    .Select(t => t.TopicName)
                    .FirstOrDefaultAsync()
                : null;

            // -------- Filename helpers --------

            string Safe(string? value) =>
                string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : string.Concat(
                        value.Split(Path.GetInvalidFileNameChars())
                    ).Replace(" ", "_");

            var parts = new List<string>
                {
                    "ExportedPYQs"
                };

            if (!string.IsNullOrWhiteSpace(examName))
                parts.Add(Safe(examName));

            if (!string.IsNullOrWhiteSpace(yearName))
                parts.Add(Safe(yearName));
            else
                parts.Add("AllYears");

            if (!string.IsNullOrWhiteSpace(subjectName))
                parts.Add(Safe(subjectName));

            if (!string.IsNullOrWhiteSpace(topicName))
                parts.Add(Safe(topicName));

            var datePart = DateTime.UtcNow.ToString("dd-MM-yyyy");
            parts.Add(datePart);

            var fileName = $"{string.Join("_", parts)}.xlsx";

            // -------- Excel generation --------

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("Questions");

            var headers = new[]
            {
                "QuestionId", "ExamId", "SubjectId", "TopicId", "YearId",
                "QuestionText", "OptionA", "OptionB", "OptionC", "OptionD",
                "CorrectOption", "Explanation", "Source",
                "SourceType", "DifficultyLevel", "Nature",
                "Motivation", "IsOfficialAnswer"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            }

            for (int i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                int row = i + 2;

                worksheet.Cells[row, 1].Value = q.QuestionId;
                worksheet.Cells[row, 2].Value = q.ExamId;
                worksheet.Cells[row, 3].Value = q.SubjectId;
                worksheet.Cells[row, 4].Value = q.TopicId;
                worksheet.Cells[row, 5].Value = q.YearId;
                worksheet.Cells[row, 6].Value = q.QuestionText;
                worksheet.Cells[row, 7].Value = q.OptionA;
                worksheet.Cells[row, 8].Value = q.OptionB;
                worksheet.Cells[row, 9].Value = q.OptionC;
                worksheet.Cells[row, 10].Value = q.OptionD;
                worksheet.Cells[row, 11].Value = q.CorrectOption;
                worksheet.Cells[row, 12].Value = q.Explanation;
                worksheet.Cells[row, 13].Value = q.Source;
                worksheet.Cells[row, 14].Value = q.SourceType?.ToString();
                worksheet.Cells[row, 15].Value = q.DifficultyLevel?.ToString();
                worksheet.Cells[row, 16].Value = q.Nature?.ToString();
                worksheet.Cells[row, 17].Value = q.Motivation;
                worksheet.Cells[row, 18].Value = q.IsOfficialAnswer;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return File(
                new MemoryStream(package.GetAsByteArray()),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }


        [Authorize(Roles = RoleConstant.Admin)]
        [HttpGet("excel-template")]
        public IActionResult DownloadExcelTemplate()
        {
            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("QuestionsTemplate");

            var headers = new[]
            {
                "QuestionId (leave blank for insert)",
                "ExamId",
                "SubjectId *",
                "TopicId",
                "YearId",
                "QuestionText *",
                "OptionA *",
                "OptionB *",
                "OptionC *",
                "OptionD *",
                "CorrectOption *",
                "Explanation",
                "Source",
                "SourceType",
                "DifficultyLevel",
                "Nature",
                "Motivation",
                "IsOfficialAnswer"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            }

            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            return File(
                new MemoryStream(package.GetAsByteArray()),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "Questions_BulkUpload_Template.xlsx"
            );
        }

        private static readonly List<QuestionImageRow> EmptyQuestionImageRows = new();

        private sealed class QuestionImageRow
        {
            public int QuestionId { get; init; }
            public int ImageId { get; init; }
            public string BucketKey { get; init; } = string.Empty;
            public ImageType ImageType { get; init; }
        }

        private async Task<bool> IsPremiumUserAsync(int? userId, bool isAdmin)
        {
            if (isAdmin)
                return true;

            if (!userId.HasValue)
                return false;

            var cacheKey = $"user-premium-status:{userId.Value}";
            if (_cache.TryGetValue(cacheKey, out bool cachedPremiumStatus))
                return cachedPremiumStatus;

            var isPremium = await _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.UserId == userId
                            && o.Status == OrderStatus.Paid
                            && o.ExpiresAt != null
                            && o.ExpiresAt > DateTime.UtcNow);

            _cache.Set(cacheKey, isPremium, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30),
                Size = 1
            });

            return isPremium;
        }

        private async Task<List<int>?> GetSelectedExamIdsAsync(int userId)
        {
            var cacheKey = $"user-selected-exam-ids:{userId}";

            if (_cache.TryGetValue(cacheKey, out List<int>? cachedIds))
                return cachedIds;

            var selectedExamIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.SelectedExamIds)
                .FirstOrDefaultAsync();

            if (selectedExamIds == null)
                return null;

            var selectedExamIdsCopy = selectedExamIds.ToList();
            _cache.Set(cacheKey, selectedExamIdsCopy, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
                Size = 1
            });

            return selectedExamIdsCopy;
        }

        private async Task<Dictionary<int, List<QuestionImageRow>>> GetQuestionImagesByQuestionIdAsync(List<int> questionIds)
        {
            if (questionIds.Count == 0)
                return new Dictionary<int, List<QuestionImageRow>>();

            var images = await _context.QuestionAnswerImages
                .AsNoTracking()
                .Where(qa => questionIds.Contains(qa.QuestionId))
                .Select(qa => new QuestionImageRow
                {
                    QuestionId = qa.QuestionId,
                    ImageId = qa.Image.ImageId,
                    BucketKey = qa.Image.BucketKey,
                    ImageType = qa.Image.ImageType
                })
                .ToListAsync();

            return images
                .GroupBy(i => i.QuestionId)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private void ShuffleInPlace(List<int> values)
        {
            for (var i = values.Count - 1; i > 0; i--)
            {
                var swapIndex = Random.Shared.Next(i + 1);
                (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
            }
        }

        private object? BuildImageDto(List<QuestionImageRow> images, ImageType type)
        {
            var image = images.FirstOrDefault(i => i.ImageType == type);
            if (image == null)
                return null;

            return new
            {
                Url = _mediaUrl.Build(image.BucketKey),
                ImageId = image.ImageId
            };
        }

        private List<object> BuildAnswerImageDtos(List<QuestionImageRow> images)
        {
            return images
                .Where(i => i.ImageType == ImageType.Answer)
                .Select(i => (object)new
                {
                    Url = _mediaUrl.Build(i.BucketKey),
                    ImageId = i.ImageId
                })
                .ToList();
        }

        private async Task<object?> GetQuestions(int questionId)
        {
            // Fetch the question with related entities
            var q = await _context.Questions
                .AsNoTracking()
                .Include(x => x.Exam)
                .Include(x => x.Subject)
                .Include(x => x.Topic)
                .Include(x => x.Year)
                .FirstOrDefaultAsync(x => x.QuestionId == questionId);

            if (q == null) return null;

            // Fetch all related images
            var images = await _context.QuestionAnswerImages
                .AsNoTracking()
                .Where(qa => qa.QuestionId == questionId)
                .Select(qa => new QuestionImageRow
                {
                    QuestionId = qa.QuestionId,
                    ImageId = qa.Image.ImageId,
                    BucketKey = qa.Image.BucketKey,
                    ImageType = qa.Image.ImageType
                })
                .ToListAsync();

            var enumLabelMap = await GetEnumLabelMapAsync();

            string GetEnumLabel(string enumType, string enumName)
            {
                return enumLabelMap.TryGetValue((enumType, enumName), out var label)
                    ? label
                    : enumName;
            }

            object? BuildEnumDto(Enum? e, string enumType)
            {
                if (e == null) return null;

                var key = e.ToString();

                return new
                {
                    Value = Convert.ToInt32(e),
                    Key = key,
                    Label = GetEnumLabel(enumType, key)
                };
            }

            object? GetImageDto(ImageType type)
            {
                var img = images
                    .FirstOrDefault(i => i.ImageType == type);

                if (img == null) return null;

                return new
                {
                    Url = _mediaUrl.Build(img.BucketKey),
                    ImageId = img.ImageId
                };
            }

            var answerImages = images
                .Where(i => i.ImageType == ImageType.Answer)
                .Select(i => new
                {
                    Url = _mediaUrl.Build(i.BucketKey),
                    ImageId = i.ImageId
                })
                .ToList();

            // Final response (EXACTLY like filter endpoint)
            return new
            {
                q.QuestionId,
                q.QuestionText,
                q.OptionA,
                q.OptionB,
                q.OptionC,
                q.OptionD,
                q.CorrectOption,
                q.Explanation,

                QuestionImage = GetImageDto(ImageType.Question),
                OptionAImage = GetImageDto(ImageType.OptionA),
                OptionBImage = GetImageDto(ImageType.OptionB),
                OptionCImage = GetImageDto(ImageType.OptionC),
                OptionDImage = GetImageDto(ImageType.OptionD),
                AnswerImages = answerImages,

                q.Source,
                SourceType = BuildEnumDto(q.SourceType, "SourceType"),
                DifficultyLevel = BuildEnumDto(q.DifficultyLevel, "DifficultyLevel"),
                Nature = BuildEnumDto(q.Nature, "Nature"),

                q.Motivation,
                q.IsDeleted,
                q.IsOfficialAnswer,
                q.CreatedAt,
                q.UpdatedAt,

                q.ExamId,
                ExamName = q.Exam?.ShortName ?? "Unknown",

                q.SubjectId,
                SubjectName = q.Subject?.SubjectName ?? "Unknown",

                q.TopicId,
                TopicName = q.Topic?.TopicName ?? "Unknown",

                q.YearId,
                YearName = q.Year?.YearName ?? "Unknown"
            };
        }
        private async Task<Dictionary<(string EnumType, string EnumName), string>> GetEnumLabelMapAsync()
        {
            return await _cache.GetOrCreateAsync("question-enum-labels", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
                entry.Size = 1;

                var labels = await _context.EnumLabels
                    .AsNoTracking()
                    .Select(e => new { e.EnumType, e.EnumName, e.DisplayLabel })
                    .ToListAsync();

                return labels.ToDictionary(
                    e => (e.EnumType, e.EnumName),
                    e => e.DisplayLabel);
            }) ?? new Dictionary<(string EnumType, string EnumName), string>();
        }

        // Shared counter for cache-key versioning. Interlocked.Increment is
        // atomic under concurrent requests, avoiding a lost-update race where
        // two threads both read the old value and write the same incremented value.
        private static int _examCandidateVersion = 0;

        private void BumpExamCandidateVersion()
        {
            var newVersion = Interlocked.Increment(ref _examCandidateVersion);
            _cache.Set(CacheKeys.ExamCandidateVersion, newVersion, new MemoryCacheEntryOptions { Size = 1 });
        }
    }
}

