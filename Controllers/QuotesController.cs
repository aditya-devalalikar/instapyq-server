using Microsoft.AspNetCore.Authorization;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.QuoteRequests;
using pqy_server.Models.Quotes;
using pqy_server.Services;
using pqy_server.Shared;


namespace pqy_server.Controllers
{
    [Authorize] // Any authenticated user can access
    [ApiController]
    [Route("api/[controller]")]
    public class QuotesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storage;
        private readonly MediaUrlBuilder _mediaUrl;

        public QuotesController(AppDbContext context, IStorageService storage, MediaUrlBuilder mediaUrl)
        {
            _context = context;
            _storage = storage;
            _mediaUrl = mediaUrl;
        }

        // 📥 GET: /api/quotes
        [HttpGet]
        public IActionResult GetAll()
        {
            var quotes = _context.Quotes.ToList();

            var result = quotes.Select(q => new
            {
                q.Id,
                q.Text,
                q.Author,
                q.CreatedAt,
                ImageUrl = string.IsNullOrEmpty(q.ImageUrl) ? null : _mediaUrl.Build(q.ImageUrl)
            });

            return Ok(ApiResponse<object>.Success(result, "Quotes retrieved successfully."));
        }

        [HttpGet("{id}")]
        public IActionResult GetById(int id)
        {
            var quote = _context.Quotes.FirstOrDefault(q => q.Id == id);
            if (quote == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Quote not found."));

            var result = new
            {
                quote.Id,
                quote.Text,
                quote.Author,
                quote.CreatedAt,
                ImageUrl = string.IsNullOrEmpty(quote.ImageUrl) ? null : _mediaUrl.Build(quote.ImageUrl)
            };

            return Ok(ApiResponse<object>.Success(result, "Quote retrieved successfully."));
        }

        // ➕ POST: /api/quotes
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] QuoteCreateRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Quote text is required."));

            var quote = new Quote
            {
                Text = request.Text,
                Author = request.Author,
                CreatedAt = DateTime.UtcNow
            };

            // Upload image if present
            if (request.File != null && request.File.Length > 0)
            {
                var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
                var safeName = Path.GetFileNameWithoutExtension(request.File.FileName)
                    .ToLowerInvariant()
                    .Replace(" ", "-");
                var key = $"{Guid.NewGuid()}_{safeName}{ext}";
                await _storage.UploadFileAsync(request.File, key);
                quote.ImageUrl = key; // store storage key here
            }

            _context.Quotes.Add(quote);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(quote, "Quote created."));
        }


        // ✏️ PUT: /api/quotes/{id}
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(int id, [FromForm] QuoteUpdateRequest request)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Quote not found."));

            quote.Text = request.Text;
            quote.Author = request.Author;

            if (request.File != null && request.File.Length > 0)
            {
                if (!string.IsNullOrEmpty(quote.ImageUrl))
                    await _storage.DeleteAsync(quote.ImageUrl);

                var ext = Path.GetExtension(request.File.FileName).ToLowerInvariant();
                var safeName = Path.GetFileNameWithoutExtension(request.File.FileName)
                    .ToLowerInvariant()
                    .Replace(" ", "-");
                var key = $"{Guid.NewGuid()}_{safeName}{ext}";
                await _storage.UploadFileAsync(request.File, key);
                quote.ImageUrl = key;
            }

            _context.Entry(quote).State = EntityState.Modified;
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(quote, "Quote updated."));
        }


        // ❌ DELETE: /api/quotes/{id}
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var quote = await _context.Quotes.FindAsync(id);
            if (quote == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Quote not found."));

            if (!string.IsNullOrEmpty(quote.ImageUrl))
                await _storage.DeleteAsync(quote.ImageUrl);

            _context.Quotes.Remove(quote);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Quote deleted."));
        }
    }
}

