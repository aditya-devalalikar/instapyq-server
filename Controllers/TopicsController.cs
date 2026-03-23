using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Order;
using pqy_server.Models.Topic;
using pqy_server.Models.Topics;
using pqy_server.Shared;
using System.Security.Claims;

namespace pqy_server.Controllers
{
    [Authorize] // 👤 Any authenticated user access
    [ApiController]
    [Route("api/[controller]")]
    [OutputCache(PolicyName = "LookupPolicy")]
    public class TopicsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IOutputCacheStore _cache;

        public TopicsController(AppDbContext context, IOutputCacheStore cache)
        {
            _context = context;
            _cache = cache;
        }

        // 📥 GET: /api/topics
        // Returns all topics along with their associated subject names, ordered by topic order.
        // For non-admins: question count is scoped to the user's selected exams,
        // and only topics with at least one question are returned.
        [HttpGet]
        [OutputCache(NoStore = true)] // Per-user response — cannot be shared
        public async Task<IActionResult> GetAll()
        {
            var (isAdmin, isPremiumUser, selectedExamIds) = await GetUserContext();

            // Pre-aggregate counts in a single GROUP BY query instead of a correlated subquery per topic
            var questionCounts = await _context.Questions
                .Where(q =>
                    (isAdmin || (q.ExamId != null && selectedExamIds.Contains(q.ExamId.Value))) &&
                    (isPremiumUser || (q.Year != null && !q.Year.IsPremium)))
                .GroupBy(q => q.TopicId)
                .Select(g => new { TopicId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TopicId ?? 0, x => x.Count);

            var rawTopics = await (
                from t in _context.Topics
                join s in _context.Subjects on t.SubjectId equals s.SubjectId
                orderby t.TopicOrder
                select new { t.TopicId, t.TopicName, t.SubjectId, t.TopicOrder, s.SubjectName }
            ).ToListAsync();

            var topics = rawTopics
                .Select(t => new Topic
                {
                    TopicId    = t.TopicId,
                    TopicName  = t.TopicName,
                    SubjectId  = t.SubjectId,
                    TopicOrder = t.TopicOrder,
                    SubjectName = t.SubjectName,
                    QuestionCount = questionCounts.GetValueOrDefault(t.TopicId, 0)
                })
                .Where(t => isAdmin || t.QuestionCount > 0)
                .ToList();

            return Ok(ApiResponse<object>.Success(topics));
        }

        // 📥 GET: /api/topics/by-subject/{subjectId}
        // Returns topics filtered by subject, ordered by topic order.
        [HttpGet("by-subject/{subjectId}")]
        [OutputCache(NoStore = true)] // Per-user response — cannot be shared
        public async Task<IActionResult> GetTopicsBySubject(int subjectId)
        {
            var (isAdmin, isPremiumUser, selectedExamIds) = await GetUserContext();

            // Pre-aggregate counts in a single GROUP BY query instead of a correlated subquery per topic
            var questionCounts = await _context.Questions
                .Where(q =>
                    q.SubjectId == subjectId &&
                    (isAdmin || (q.ExamId != null && selectedExamIds.Contains(q.ExamId.Value))) &&
                    (isPremiumUser || (q.Year != null && !q.Year.IsPremium)))
                .GroupBy(q => q.TopicId)
                .Select(g => new { TopicId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.TopicId ?? 0, x => x.Count);

            var rawTopics = await _context.Topics
                .Where(t => t.SubjectId == subjectId)
                .OrderBy(t => t.TopicOrder)
                .Select(t => new { t.TopicId, t.TopicName, t.SubjectId, t.TopicOrder })
                .ToListAsync();

            var topics = rawTopics
                .Select(t => new Topic
                {
                    TopicId    = t.TopicId,
                    TopicName  = t.TopicName,
                    SubjectId  = t.SubjectId,
                    TopicOrder = t.TopicOrder,
                    QuestionCount = questionCounts.GetValueOrDefault(t.TopicId, 0)
                })
                .Where(t => isAdmin || t.QuestionCount > 0)
                .ToList();

            return Ok(ApiResponse<object>.Success(topics));
        }

        private async Task<(bool isAdmin, bool isPremiumUser, List<int> selectedExamIds)> GetUserContext()
        {
            bool isAdmin = User.IsInRole(RoleConstant.Admin);
            if (isAdmin)
                return (true, true, new List<int>());

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return (false, false, new List<int>());

            var selectedExamIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.SelectedExamIds)
                .FirstOrDefaultAsync() ?? new List<int>();

            var isPremiumUser = await _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.UserId == userId
                            && o.Status == OrderStatus.Paid
                            && o.ExpiresAt != null
                            && o.ExpiresAt > DateTime.UtcNow);

            return (false, isPremiumUser, selectedExamIds);
        }


        // ➕ POST: /api/topics
        // Admin only endpoint to create a new topic
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateTopicRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.TopicName))
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.ValidationError, "Topic name is required."
                ));

            // ✅ Auto order ONLY within same subject
            int nextOrder = await _context.Topics
                .Where(t => t.SubjectId == request.SubjectId)
                .Select(t => (int?)t.TopicOrder)
                .MaxAsync() ?? 0;

            var topic = new Topic
            {
                TopicName = request.TopicName.Trim(),
                SubjectId = request.SubjectId,
                TopicOrder = nextOrder + 1
            };

            _context.Topics.Add(topic);
            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<object>.Success(topic, "Topic created."));
        }

        // ✏️ PUT: /api/topics/{id}
        // Admin only endpoint to update an existing topic
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateTopicRequest request)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Topic not found."));

            if (string.IsNullOrWhiteSpace(request.TopicName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Topic name is required."));

            topic.TopicName = request.TopicName;

            if (request.SubjectId.HasValue)
                topic.SubjectId = request.SubjectId.Value;

            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<object>.Success(topic, "Topic updated."));
        }

        // ❌ DELETE: /api/topics/{id}
        // Admin only endpoint to delete a topic
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null)
            {
                return NotFound(
                    ApiResponse<string>.Failure(
                        ResultCode.NotFound,
                        "Topic not found."
                    )
                );
            }

            // ✅ CHECK: Is topic used by any question?
            bool hasQuestions = await _context.Questions
                .AnyAsync(q => q.TopicId == id && !q.IsDeleted);

            if (hasQuestions)
            {
                return BadRequest(
                    ApiResponse<string>.Failure(
                        ResultCode.TopicHasQuestions,
                        "Cannot delete topic because questions are associated with it."
                    )
                );
            }

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(
                ApiResponse<string>.Success("Topic deleted successfully.")
            );
        }


        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPut("{id}/reorder")]
        public async Task<IActionResult> Reorder(int id, [FromBody] ReorderTopicRequest request)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Topic not found."));

            int oldOrder = topic.TopicOrder;
            int newOrder = request.NewOrder;

            if (oldOrder == newOrder)
                return Ok(ApiResponse<object>.Success(topic));

            var topics = await _context.Topics
                .Where(t => t.SubjectId == topic.SubjectId)
                .OrderBy(t => t.TopicOrder)
                .ToListAsync();

            // ✅ STEP 1: move current topic out of the way
            topic.TopicOrder = -1;
            await _context.SaveChangesAsync();

            foreach (var t in topics)
            {
                if (t.TopicId == topic.TopicId) continue;

                if (newOrder > oldOrder &&
                    t.TopicOrder > oldOrder &&
                    t.TopicOrder <= newOrder)
                {
                    t.TopicOrder--;
                }

                if (newOrder < oldOrder &&
                    t.TopicOrder >= newOrder &&
                    t.TopicOrder < oldOrder)
                {
                    t.TopicOrder++;
                }
            }

            // ✅ STEP 2: assign final order
            topic.TopicOrder = newOrder;

            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<object>.Success(topic, "Topic reordered."));
        }

    }
}
