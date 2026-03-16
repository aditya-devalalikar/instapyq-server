using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Data;
using pqy_server.Models.Bookmark;
using pqy_server.Shared;
using System.Security.Claims;
using Serilog;

namespace pqy_server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BookmarksController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BookmarksController(AppDbContext context)
        {
            _context = context;
        }

        // ➕ POST: /api/bookmarks/{questionId}
        [HttpPost("{questionId}")]
        public async Task<IActionResult> AddBookmark(
            int questionId,
            [FromBody] AddBookmarkRequest request
        )
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.Unauthorized, "Invalid user ID."
                ));

            var exists = await _context.Bookmarks
                .AnyAsync(b => b.UserId == userId && b.QuestionId == questionId);

            if (exists)
                return BadRequest(ApiResponse<string>.Failure(
                    ResultCode.Conflict, "Already bookmarked."
                ));

            var question = await _context.Questions.FindAsync(questionId);
            if (question == null)
                return NotFound(ApiResponse<string>.Failure(
                    ResultCode.NotFound, "Question not found."
                ));

            var bookmark = new BookmarkQuestion
            {
                UserId = userId,
                QuestionId = questionId,
                SubjectId = request.SubjectId ?? question.SubjectId,
                TopicId = request.TopicId ?? question.TopicId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Bookmarks.Add(bookmark);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Bookmarked"));
        }

        // ❌ DELETE: /api/bookmarks/{questionId}
        [HttpDelete("{questionId}")]
        public async Task<IActionResult> RemoveBookmark(int questionId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.Unauthorized, "Invalid user ID."
                ));

            var bookmark = await _context.Bookmarks
                .FirstOrDefaultAsync(b =>
                    b.UserId == userId && b.QuestionId == questionId
                );

            if (bookmark == null)
                return NotFound(ApiResponse<string>.Failure(
                    ResultCode.NotFound, "Bookmark not found."
                ));

            _context.Bookmarks.Remove(bookmark);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Bookmark removed"));
        }

        // 📊 GET: /api/bookmarks/by-subject
        [HttpGet("by-subject")]
        public async Task<IActionResult> GetBookmarksGroupedBySubject()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.Unauthorized, "Invalid user ID."
                ));

            var grouped = await _context.Bookmarks
                .AsNoTracking()
                .Where(b => b.UserId == userId && b.Question != null)
                .Include(b => b.Question)
                    .ThenInclude(q => q.Subject)
                .GroupBy(b => new
                {
                    b.Question.SubjectId,
                    b.Question.Subject.SubjectName
                })
                .Select(g => new
                {
                    SubjectId = g.Key.SubjectId,
                    SubjectName = g.Key.SubjectName,
                    BookmarkCount = g.Count()
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(grouped));
        }

        // 📊 GET: /api/bookmarks/by-subject/{subjectId}/by-topic
        [HttpGet("by-subject/{subjectId}/by-topic")]
        public async Task<IActionResult> GetBookmarksByTopicForSubject(int subjectId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(
                    ResultCode.Unauthorized, "Invalid user ID."
                ));

            var grouped = await _context.Bookmarks
                .AsNoTracking()
                .Where(b =>
                    b.UserId == userId &&
                    b.Question.SubjectId == subjectId &&
                    b.Question.Topic != null
                )
                .Include(b => b.Question)
                    .ThenInclude(q => q.Topic)
                .GroupBy(b => new
                {
                    b.Question.TopicId,
                    b.Question.Topic.TopicName
                })
                .Select(g => new
                {
                    TopicId = g.Key.TopicId,
                    TopicName = g.Key.TopicName,
                    BookmarkCount = g.Count()
                })
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(grouped));
        }
    }
}
