using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Data;
using pqy_server.DTOs.QuestionNotes;
using pqy_server.Models.QuestionNotes;
using pqy_server.Shared;
using System.Security.Claims;

namespace pqy_server.Controllers
{
    [Authorize] // 🔐 Requires authenticated user
    [ApiController]
    [Route("api/[controller]")]
    public class QuestionNotesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public QuestionNotesController(AppDbContext context)
        {
            _context = context;
        }

        // ➕ POST: /api/questionnotes/{questionId}
        // Adds a new note for a specified question by the authenticated user
        [HttpPost("{questionId}")]
        public async Task<IActionResult> AddNote(int questionId, [FromBody] CreateNoteRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID."));

            // Check if note already exists for this question and user
            var existing = await _context.QuestionNotes.FirstOrDefaultAsync(
                n => n.QuestionId == questionId && n.UserId == userId);
            if (existing != null)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Note already exists for this question."));

            var note = new QuestionNote
            {
                UserId = userId,
                QuestionId = questionId,
                Content = request.Content,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.QuestionNotes.Add(note);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Note added successfully."));
        }

        // 📝 PUT: /api/questionnotes/{noteId}
        // Updates the content of an existing note belonging to the authenticated user
        [HttpPut("{noteId}")]
        public async Task<IActionResult> UpdateNote(int noteId, [FromBody] UpdateNoteRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID."));

            var note = await _context.QuestionNotes.FirstOrDefaultAsync(n => n.NoteId == noteId && n.UserId == userId);
            if (note == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Note not found."));

            note.Content = request.Content;
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Note updated."));
        }

        // ❌ DELETE: /api/questionnotes/{noteId}
        // Deletes the note specified by noteId if it belongs to the authenticated user
        [HttpDelete("{noteId}")]
        public async Task<IActionResult> DeleteNote(int noteId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID."));

            var note = await _context.QuestionNotes.FirstOrDefaultAsync(n => n.NoteId == noteId && n.UserId == userId);
            if (note == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Note not found."));

            _context.QuestionNotes.Remove(note);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Note deleted."));
        }

        // 📋 GET: /api/questionnotes/{questionId}
        // Retrieves the note content for a specific question owned by the authenticated user
        [HttpGet("{questionId}")]
        public async Task<IActionResult> GetMyNote(int questionId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized(ApiResponse<string>.Failure(ResultCode.Unauthorized, "Invalid user ID."));

            var note = await _context.QuestionNotes.FirstOrDefaultAsync(n => n.QuestionId == questionId && n.UserId == userId);
            if (note == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Note not found."));

            // Return note details wrapped in ApiResponse
            var result = new
            {
                note.NoteId,
                note.Content,
                note.CreatedAt,
                note.UpdatedAt
            };

            return Ok(ApiResponse<object>.Success(result));
        }
    }
}
