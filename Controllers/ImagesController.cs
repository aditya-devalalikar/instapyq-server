using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Images;
using pqy_server.Services;
using pqy_server.Shared;
using Serilog;

namespace pqy_server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ImagesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storage;
        private readonly MediaUrlBuilder _mediaUrl;

        public ImagesController(AppDbContext context, IStorageService storage, MediaUrlBuilder mediaUrl)
        {
            _context = context;
            _storage = storage;
            _mediaUrl = mediaUrl;
        }

        /// <summary>
        /// Upload an image for question, option, or answer
        /// </summary>
        /// <param name="request">File to upload</param>
        /// <param name="questionId">Optional: the question this image is for</param>
        /// <param name="type">"question", "optionA", "optionB", "optionC", "optionD", "answer"</param>
        [HttpPost("upload")]
        [Authorize(Roles = RoleConstant.Admin)]
        [RequestSizeLimit(5_242_880)] // 5 MB
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Upload([FromForm] UploadImageRequest request)
        {
            if (request?.File == null || request.File.Length == 0)
                return BadRequest(
                    ApiResponse<string>.Failure(ResultCode.BadRequest, "No file uploaded.")
                );

            // Validate MIME type whitelist
            var allowedMimeTypes = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
            if (!allowedMimeTypes.Contains(request.File.ContentType, StringComparer.OrdinalIgnoreCase))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "Only image files (jpeg, png, webp, gif) are allowed."));

            // Validate actual file content via magic bytes
            byte[] header = new byte[12];
            using (var peekStream = request.File.OpenReadStream())
                await peekStream.ReadAsync(header.AsMemory(0, Math.Min(12, (int)request.File.Length)));

            bool isValidImage =
                (header[0] == 0xFF && header[1] == 0xD8) ||                                                  // JPEG
                (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47) ||       // PNG
                (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46) ||                             // GIF
                (header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50);        // WebP

            if (!isValidImage)
                return BadRequest(ApiResponse<string>.Failure(ResultCode.BadRequest, "File content does not match an allowed image format."));

            try
            {
                var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
                if (!new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }.Contains(ext)) ext = ".jpg";
                var safeName = System.Text.RegularExpressions.Regex.Replace(
                    Path.GetFileNameWithoutExtension(request.File.FileName), @"[^a-zA-Z0-9]", "-").ToLowerInvariant();
                var shortId = Guid.NewGuid().ToString("N")[..8];
                var key = $"{shortId}_{safeName}{ext}";
                await _storage.UploadFileAsync(request.File, key);

                // 🔹 Save image metadata
                var image = new ImageFile
                {
                    FileName = Path.GetFileName(request.File.FileName), // sanitized name only
                    BucketKey = key,
                    EntityType = request.QuestionId.HasValue ? "Question" : "Other",
                    EntityId = request.QuestionId ?? 0,
                    ImageType = request.Type
                };

                _context.Images.Add(image);
                await _context.SaveChangesAsync();

                // 🔹 Link image to question
                if (request.QuestionId.HasValue)
                {
                    var questionExists = await _context.Questions
                        .AnyAsync(q => q.QuestionId == request.QuestionId.Value);

                    if (!questionExists)
                        return NotFound(
                            ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found.")
                        );

                    _context.QuestionAnswerImages.Add(new QuestionAnswerImage
                    {
                        QuestionId = request.QuestionId.Value,
                        ImageId = image.ImageId
                    });

                    await _context.SaveChangesAsync();
                }

                // ✅ RETURN PROXIED URL (THIS FIXES UI)
                return Ok(
                    ApiResponse<object>.Success(new
                    {
                        imageId = image.ImageId,
                        url = _mediaUrl.Build(image.BucketKey)
                    }, "Image uploaded successfully.")
                );
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Image upload failed.");
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while uploading the image."));
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var image = await _context.Images.FindAsync(id);
            if (image == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Image not found."));

            try
            {
                await _storage.DeleteAsync(image.BucketKey);

                // Remove any links to questions
                var links = _context.Set<QuestionAnswerImage>().Where(q => q.ImageId == id);
                _context.Set<QuestionAnswerImage>().RemoveRange(links);

                _context.Images.Remove(image);
                await _context.SaveChangesAsync();

                return Ok(ApiResponse<string>.Success("Image deleted successfully."));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Image delete failed. iid={iid}", id);
                return StatusCode(500, ApiResponse<string>.Failure(ResultCode.InternalServerError, "An error occurred while deleting the image."));
            }
        }

        [HttpGet]
        [Authorize(Roles = RoleConstant.Admin)]
        public async Task<IActionResult> GetAll()
        {
            var images = await _context.Images
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var result = images.Select(img => new
            {
                img.ImageId,
                img.FileName,
                img.BucketKey,
                Url = _storage.GetPresignedFileUrl(img.BucketKey, 10), // 🔹 signed URL for frontend
                img.CreatedAt,
                img.UpdatedAt
            });
            return Ok(ApiResponse<object>.Success(result));
        }
    }
}
