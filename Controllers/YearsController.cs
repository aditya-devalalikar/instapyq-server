using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Years;
using pqy_server.Models.Order;
using pqy_server.Services;
using pqy_server.Shared;
using Serilog;
using System.Security.Claims;

namespace pqy_server.Controllers
{
    [Authorize] // 👤 Only authenticated users can access any endpoint
    [ApiController]
    [Route("api/[controller]")]
    public class YearsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storage;
        private readonly MediaUrlBuilder _mediaUrl;

        // 🔹 Constructor: injects AppDbContext for database access
        public YearsController(AppDbContext context, IStorageService storage, MediaUrlBuilder mediaUrl)
        {
            _context = context;
            _storage = storage;
            _mediaUrl = mediaUrl;
        }

        /// <summary>
        /// GET: /api/years
        /// Retrieves all years from the database, optionally filtered by ExamId.
        /// Only returns years that are not soft-deleted.
        /// Includes the Exam navigation property for examName.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] int? examId)
        {
            var isAdmin = User.IsInRole("Admin");
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            int? userId = int.TryParse(userIdClaim, out var uid) ? uid : null;

            // Medium #8 fix: check actual premium from DB
            bool isPremiumUser = isAdmin || (userId.HasValue && await _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.UserId == userId.Value
                            && o.Status == OrderStatus.Paid
                            && o.ExpiresAt != null
                            && o.ExpiresAt > DateTime.UtcNow));

            var query = _context.Years
                .AsNoTracking()
                .Include(y => y.Exam)
                .AsQueryable();

            // ❗ Apply soft-delete filter ONLY for non-admin
            if (!isAdmin)
            {
                query = query.Where(y => !y.IsDeleted);
            }

            // Apply exam filter if provided
            if (examId.HasValue)
            {
                query = query.Where(y => y.ExamId == examId.Value);
            }

            var years = await (
                from y in query
                join q in _context.Questions
                    on y.YearId equals q.YearId into qg
                orderby y.YearOrder
                select new
                {
                    yearId = y.YearId,
                    yearName = y.YearName,
                    paperName = y.PaperName,
                    examId = y.ExamId,
                    yearOrder = y.YearOrder,
                    examName = y.Exam.ExamName,
                    isDeleted = y.IsDeleted,
                    isPremium = y.IsPremium,
                    // Medium #8 fix: gate PDF URLs behind premium/admin
                    // Store key only; URL built from Media:PublicBaseUrl at response time instead of permanent public URLs
                    questionPaperKey = y.QuestionPaperKey,
                    answerKeyKey = y.AnswerKeyKey,
                    isPremiumUser = isPremiumUser,
                    questionCount = qg.Count()
                }
            ).ToListAsync();

            // Map to final response with public CDN URLs
            var result = years.Select(y => new
            {
                y.yearId,
                y.yearName,
                y.paperName,
                y.examId,
                y.yearOrder,
                y.examName,
                y.isDeleted,
                y.isPremium,
                questionPaperUrl = (y.isPremiumUser || !y.isPremium)
                    ? (y.questionPaperKey != null ? _mediaUrl.Build(y.questionPaperKey) : null)
                    : (y.questionPaperKey != null ? "locked" : null),
                answerKeyUrl = (y.isPremiumUser || !y.isPremium)
                    ? (y.answerKeyKey != null ? _mediaUrl.Build(y.answerKeyKey) : null)
                    : (y.answerKeyKey != null ? "locked" : null),
                y.questionCount
            }).ToList();

            return Ok(ApiResponse<object>.Success(result));
        }

        /// <summary>
        /// GET: /api/years/by-exam/{examId}
        /// Retrieves all years for a specific examId.
        /// Only returns non-deleted years.
        /// </summary>
        [HttpGet("by-exam/{examId}")]
        public async Task<IActionResult> GetYearsByExamId(int examId)
        {
            var userIdClaim = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            int? userId = int.TryParse(userIdClaim, out var uid) ? uid : null;
            bool isAdmin = User.IsInRole("Admin");
            bool isPremiumUser = isAdmin || (userId.HasValue && await _context.Orders
                .AsNoTracking()
                .AnyAsync(o => o.UserId == userId.Value
                            && o.Status == OrderStatus.Paid
                            && o.ExpiresAt != null
                            && o.ExpiresAt > DateTime.UtcNow));

            var years = await (
                from y in _context.Years.AsNoTracking()
                join q in _context.Questions
                    on y.YearId equals q.YearId into qg
                where y.ExamId == examId && !y.IsDeleted
                orderby y.YearOrder descending
                select new
                {
                    yearId = y.YearId,
                    yearName = y.YearName,
                    paperName = y.PaperName,
                    examId = y.ExamId,
                    yearOrder = y.YearOrder,
                    isDeleted = y.IsDeleted,
                    isPremium = y.IsPremium,
                    // Store key only; URL built from Media:PublicBaseUrl at response time
                    questionPaperKey = y.QuestionPaperKey,
                    answerKeyKey = y.AnswerKeyKey,
                    isPremiumUser = isPremiumUser,
                    questionCount = qg.Count()
                }
            ).ToListAsync();

            // Map to final response with public CDN URLs
            var result = years.Select(y => new
            {
                y.yearId,
                y.yearName,
                y.paperName,
                y.examId,
                y.yearOrder,
                y.isDeleted,
                y.isPremium,
                questionPaperUrl = (y.isPremiumUser || !y.isPremium)
                    ? (y.questionPaperKey != null ? _mediaUrl.Build(y.questionPaperKey) : null)
                    : (y.questionPaperKey != null ? "locked" : null),
                answerKeyUrl = (y.isPremiumUser || !y.isPremium)
                    ? (y.answerKeyKey != null ? _mediaUrl.Build(y.answerKeyKey) : null)
                    : (y.answerKeyKey != null ? "locked" : null),
                y.questionCount
            }).ToList();

            return Ok(ApiResponse<object>.Success(result));
        }


        /// <summary>
        /// POST: /api/years
        /// Creates a new Year entry.
        /// Only accessible by Admin users.
        /// Validates input and ensures YearOrder uniqueness per Exam.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Create([FromBody] CreateYearRequest request)
        {
            // 🔹 Validation
            if (string.IsNullOrWhiteSpace(request.YearName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Year name is required."));

            if (string.IsNullOrWhiteSpace(request.PaperName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Paper name is required."));

            if (request.ExamId <= 0)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Valid ExamId is required."));

            // 🔹 Auto-calculate next YearOrder per Exam
            int nextYearOrder = await _context.Years
                .Where(y => y.ExamId == request.ExamId)
                .Select(y => (int?)y.YearOrder)
                .MaxAsync() ?? 0;

            nextYearOrder++;

            // 🔹 Create Year
            var year = new Year
            {
                YearName = request.YearName,
                PaperName = request.PaperName,
                ExamId = request.ExamId,
                YearOrder = nextYearOrder,
                IsDeleted = request.IsDeleted ?? false,
                IsPremium = true
            };

            _context.Years.Add(year);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(year, "Year created successfully."));
        }

        /// <summary>
        /// PUT: /api/years/{id}
        /// Updates an existing Year entry.
        /// Only accessible by Admin users.
        /// Validates input and ensures YearOrder uniqueness excluding the current record.
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateYearRequest request)
        {
            var year = await _context.Years.FindAsync(id);
            if (year == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Year not found."));

            // 🔹 Input validation
            if (string.IsNullOrWhiteSpace(request.YearName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Year name is required."));

            if (string.IsNullOrWhiteSpace(request.PaperName))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Paper name is required."));

            if (request.ExamId <= 0)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Valid ExamId is required."));

            // 🔹 Update properties
            year.YearName = request.YearName;
            year.PaperName = request.PaperName;
            year.ExamId = request.ExamId;
            year.IsDeleted = request.IsDeleted;

            // 🔹 Update IsPremium only if value provided in request; otherwise keep existing
            if (request.IsPremium.HasValue)
                year.IsPremium = request.IsPremium.Value;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(year, "Year updated successfully."));
        }

        /// <summary>
        /// DELETE: /api/years/{id}
        /// Soft deletes a Year entry by setting IsDeleted to true.
        /// Only accessible by Admin users.
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var year = await _context.Years.FindAsync(id);
            if (year == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Year not found."));

            if (year.IsDeleted)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.Conflict, "Year is already deleted."));

            // 🔹 Soft delete
            year.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Year soft deleted successfully."));
        }

        /// <summary>
        /// POST: /api/years/{id}/question-paper
        /// Uploads a question paper PDF for a specific Year.
        /// </summary>
        [HttpPost("{id}/question-paper")]
        [Authorize(Roles = RoleConstant.Admin)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(104_857_600)] // 100 MB hard cap
        public async Task<IActionResult> UploadQuestionPaper(int id, [FromForm] UploadYearPdfRequest request)
        {
            if (request?.File == null || request.File.Length == 0)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "No file uploaded."));

            // High #4 fix: validate MIME type
            var allowedMimeTypes = new[] { "application/pdf" };
            if (!allowedMimeTypes.Contains(request.File.ContentType))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Only PDF files are allowed."));

            const long MaxFileSize = 100 * 1024 * 1024;
            if (request.File.Length > MaxFileSize)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "File must be smaller than 100 MB."));

            // Magic byte validation: PDF files start with %PDF (0x25 0x50 0x44 0x46)
            byte[] pdfHeader = new byte[4];
            using (var peekStream = request.File.OpenReadStream())
                await peekStream.ReadAsync(pdfHeader.AsMemory(0, Math.Min(4, (int)request.File.Length)));
            if (pdfHeader[0] != 0x25 || pdfHeader[1] != 0x50 || pdfHeader[2] != 0x44 || pdfHeader[3] != 0x46)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "File content does not match PDF format."));

            var year = await _context.Years.Include(y => y.Exam).FirstOrDefaultAsync(y => y.YearId == id);
            if (year == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Year not found."));

            try
            {
                if (!string.IsNullOrEmpty(year.QuestionPaperKey))
                    await _storage.DeleteAsync(year.QuestionPaperKey);

                var key = BuildPdfKey(year.Exam?.ShortName, year.YearName, "QP");
                await _storage.UploadFileAsync(request.File, key);

                year.QuestionPaperKey = key;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<string>.Success("Question paper uploaded successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QP upload failed. yid={yid}", id);
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while uploading the question paper."));
            }
        }

        /// <summary>
        /// POST: /api/years/{id}/answer-key
        /// Uploads an answer key PDF for a specific Year.
        /// </summary>
        [HttpPost("{id}/answer-key")]
        [Authorize(Roles = RoleConstant.Admin)]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(104_857_600)] // 100 MB hard cap
        public async Task<IActionResult> UploadAnswerKey(int id, [FromForm] UploadYearPdfRequest request)
        {
            if (request?.File == null || request.File.Length == 0)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "No file uploaded."));

            // High #4 fix: validate MIME type
            var allowedMimeTypes = new[] { "application/pdf" };
            if (!allowedMimeTypes.Contains(request.File.ContentType))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Only PDF files are allowed."));

            const long MaxFileSize = 100 * 1024 * 1024;
            if (request.File.Length > MaxFileSize)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "File must be smaller than 100 MB."));

            // Magic byte validation: PDF files start with %PDF (0x25 0x50 0x44 0x46)
            byte[] pdfHeader = new byte[4];
            using (var peekStream = request.File.OpenReadStream())
                await peekStream.ReadAsync(pdfHeader.AsMemory(0, Math.Min(4, (int)request.File.Length)));
            if (pdfHeader[0] != 0x25 || pdfHeader[1] != 0x50 || pdfHeader[2] != 0x44 || pdfHeader[3] != 0x46)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "File content does not match PDF format."));

            var year = await _context.Years.Include(y => y.Exam).FirstOrDefaultAsync(y => y.YearId == id);
            if (year == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Year not found."));

            try
            {
                if (!string.IsNullOrEmpty(year.AnswerKeyKey))
                    await _storage.DeleteAsync(year.AnswerKeyKey);

                var key = BuildPdfKey(year.Exam?.ShortName, year.YearName, "KEY");
                await _storage.UploadFileAsync(request.File, key);

                year.AnswerKeyKey = key;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<string>.Success("Answer key uploaded successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AK upload failed. yid={yid}", id);
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while uploading the answer key."));
            }
        }

        /// <summary>
        /// DELETE: /api/years/{id}/question-paper
        /// Deletes a question paper PDF for a specific Year.
        /// </summary>
        [HttpDelete("{id}/question-paper")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> DeleteQuestionPaper(int id)
        {
            var year = await _context.Years.FindAsync(id);
            if (year == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Year not found."));

            if (string.IsNullOrEmpty(year.QuestionPaperKey))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "No question paper found for this year."));

            try
            {
                await _storage.DeleteAsync(year.QuestionPaperKey);

                year.QuestionPaperKey = null;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<string>.Success("Question paper deleted successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "QP delete failed. yid={yid}", id);
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while deleting the question paper."));
            }
        }

        /// <summary>
        /// DELETE: /api/years/{id}/answer-key
        /// Deletes an answer key PDF for a specific Year.
        /// </summary>
        [HttpDelete("{id}/answer-key")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> DeleteAnswerKey(int id)
        {
            var year = await _context.Years.FindAsync(id);
            if (year == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Year not found."));

            if (string.IsNullOrEmpty(year.AnswerKeyKey))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "No answer key found for this year."));

            try
            {
                await _storage.DeleteAsync(year.AnswerKeyKey);

                year.AnswerKeyKey = null;
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<string>.Success("Answer key deleted successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "AK delete failed. yid={yid}", id);
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while deleting the answer key."));
            }
        }

        private static string BuildPdfKey(string? examShortName, string? yearName, string suffix)
        {
            static string Sanitize(string? s) =>
                System.Text.RegularExpressions.Regex.Replace(s ?? "UNKNOWN", @"[^a-zA-Z0-9]", "_").ToUpperInvariant();

            return $"{Sanitize(examShortName)}_{Sanitize(yearName)}_{suffix}.pdf";
        }
    }
}

