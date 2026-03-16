using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Exams;
using pqy_server.Shared;

namespace pqy_server.Controllers
{
    [Authorize] // 👤 All routes require an authenticated user
    [ApiController]
    [Route("api/[controller]")]
    [OutputCache(PolicyName = "LookupPolicy")]
    public class ExamsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IOutputCacheStore _cache;

        public ExamsController(AppDbContext context, IOutputCacheStore cache)
        {
            _context = context;
            _cache = cache;
        }

        // 📥 GET: /api/exams
        // Returns a list of all non-deleted exams
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var exams = await _context.Exams
                .Where(e => !e.IsDeleted)
                .OrderBy(e => e.ExamOrder)
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(exams));
        }

        // ➕ POST: /api/exams
        // Admin-only endpoint to create a new exam
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateExamRequest request)
        {
            // Validate exam name presence
            if (string.IsNullOrWhiteSpace(request.ExamName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Exam name is required."));

            // Ensure exam order uniqueness
            bool examOrderExists = await _context.Exams.AnyAsync(e => e.ExamOrder == request.ExamOrder);
            if (examOrderExists)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, $"Exam order {request.ExamOrder} already exists."));

            var exam = new Exam
            {
                ExamName = request.ExamName,
                ShortName = request.ShortName,
                ExamOrder = request.ExamOrder,
                IsDeleted = request.IsDeleted
            };

            _context.Exams.Add(exam);
            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<object>.Success(exam, "Exam created."));
        }

        // ✏️ PUT: /api/exams/{id}
        // Admin-only endpoint to update an existing exam
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateExamRequest request)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Exam not found."));

            // Validate exam name presence
            if (string.IsNullOrWhiteSpace(request.ExamName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Exam name is required."));

            // Ensure exam order uniqueness excluding current exam
            bool examOrderExists = await _context.Exams.AnyAsync(e => e.ExamOrder == request.ExamOrder && e.ExamId != id);
            if (examOrderExists)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, $"Exam order {request.ExamOrder} already exists."));

            // Update exam properties
            exam.ExamName = request.ExamName;
            exam.ShortName = request.ShortName;
            exam.ExamOrder = request.ExamOrder;
            exam.IsDeleted = request.IsDeleted;

            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<object>.Success(exam, "Exam updated."));
        }

        // ❌ DELETE: /api/exams/{id}
        // Admin-only endpoint to soft-delete an exam
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Exam not found."));

            if (exam.IsDeleted)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Exam is already deleted."));

            exam.IsDeleted = true;
            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<string>.Success("Exam soft deleted."));
        }

        // 🔄 POST: /api/exams/{id}/restore
        // Admin-only endpoint to restore a soft-deleted exam
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("{id}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            var exam = await _context.Exams.FindAsync(id);
            if (exam == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Exam not found."));

            if (!exam.IsDeleted)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Exam is not deleted."));

            exam.IsDeleted = false;
            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<string>.Success("Exam restored."));
        }
    }
}
