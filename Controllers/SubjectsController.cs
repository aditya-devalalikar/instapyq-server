using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Subjects;
using pqy_server.Shared;

namespace pqy_server.Controllers
{
    [Authorize] // 👤 Any authenticated user
    [ApiController]
    [Route("api/[controller]")]
    [OutputCache(PolicyName = "LookupPolicy")]
    public class SubjectsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IOutputCacheStore _cache;

        public SubjectsController(AppDbContext context, IOutputCacheStore cache)
        {
            _context = context;
            _cache = cache;
        }

        // 📥 GET: /api/subjects
        // Returns all subjects including deleted ones; optionally you can restrict this based on your business logic
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            bool isAdmin = User.IsInRole(RoleConstant.Admin);

            IQueryable<Subject> query = _context.Subjects;

            // 🚫 Non-admins should not see deleted subjects
            if (!isAdmin)
            {
                query = query.Where(s => !s.IsDeleted);
            }

            var subjects = await query
                .OrderBy(s => s.SubjectOrder)
                .ToListAsync();

            return Ok(ApiResponse<object>.Success(subjects));
        }


        // ➕ POST: /api/subjects
        // Admin-only endpoint to create a new subject
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSubjectRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SubjectName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Subject name is required."));

            bool subjectOrderExists = await _context.Subjects.AnyAsync(e => e.SubjectOrder == request.SubjectOrder);
            if (subjectOrderExists)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, $"Subject order {request.SubjectOrder} already exists."));

            var subject = new Subject
            {
                SubjectName = request.SubjectName,
                SubjectOrder = request.SubjectOrder,
                IsDeleted = request.IsDeleted
            };

            _context.Subjects.Add(subject);
            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<object>.Success(subject, "Subject created."));
        }

        // ✏️ PUT: /api/subjects/{id}
        // Admin-only endpoint to update a subject
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateSubjectRequest request)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Subject not found."));

            if (string.IsNullOrWhiteSpace(request.SubjectName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Subject name is required."));

            bool subjectOrderExists = await _context.Subjects
                .AnyAsync(e => e.SubjectOrder == request.SubjectOrder && e.SubjectId != id);
            if (subjectOrderExists)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, $"Subject order {request.SubjectOrder} already exists."));

            subject.SubjectName = request.SubjectName;
            subject.SubjectOrder = request.SubjectOrder;
            subject.IsDeleted = request.IsDeleted;

            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<object>.Success(subject, "Subject updated."));
        }

        // ❌ DELETE: /api/subjects/{id}
        // Admin-only endpoint to soft delete a subject
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Subject not found."));

            if (subject.IsDeleted)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Subject is already deleted."));

            subject.IsDeleted = true;
            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<string>.Success("Subject soft deleted."));
        }

        // 🔄 POST: /api/subjects/{id}/restore
        // Admin-only endpoint to restore a soft-deleted subject
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost("{id}/restore")]
        public async Task<IActionResult> Restore(int id)
        {
            var subject = await _context.Subjects.FindAsync(id);
            if (subject == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Subject not found."));

            if (!subject.IsDeleted)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Subject is not deleted."));

            subject.IsDeleted = false;
            await _context.SaveChangesAsync();

            await _cache.EvictByTagAsync("lookup", default);

            return Ok(ApiResponse<string>.Success("Subject restored."));
        }
    }
}
