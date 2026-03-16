using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Content;
using pqy_server.Shared;
using System.Security.Claims;
using System.Text.Json;

namespace pqy_server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ContentController : ControllerBase
    {
        private static readonly HashSet<string> SupportedSlugs = new(StringComparer.OrdinalIgnoreCase)
        {
            "faqs",
            "privacy-policy",
            "terms-conditions",
            "about-us"
        };

        private readonly AppDbContext _context;

        public ContentController(AppDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet("{slug}")]
        public async Task<IActionResult> GetBySlug(string slug)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (!SupportedSlugs.Contains(normalizedSlug))
            {
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Content page not found."));
            }

            var page = await _context.ContentPages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Slug == normalizedSlug && p.IsPublished);

            if (page == null)
            {
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Content page not found."));
            }

            return Ok(ApiResponse<object>.Success(ToDto(page), "Content fetched successfully."));
        }

        [Authorize(Roles = RoleConstant.Admin)]
        [HttpGet]
        public async Task<IActionResult> GetAllForAdmin()
        {
            var pages = await _context.ContentPages
                .AsNoTracking()
                .OrderBy(p => p.Slug)
                .ToListAsync();

            var result = pages.Select(ToDto).ToList();
            return Ok(ApiResponse<object>.Success(result, "Content pages fetched successfully."));
        }

        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPut("{slug}")]
        public async Task<IActionResult> UpsertBySlug(string slug, [FromBody] UpsertContentPageRequest request)
        {
            var normalizedSlug = NormalizeSlug(slug);
            if (!SupportedSlugs.Contains(normalizedSlug))
            {
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Unsupported content slug."));
            }

            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Title is required."));
            }

            if (normalizedSlug == "faqs" && !string.IsNullOrWhiteSpace(request.ContentJson))
            {
                try
                {
                    using var _ = JsonDocument.Parse(request.ContentJson);
                }
                catch
                {
                    return BadRequest(ApiResponse<string>.Failure(ResultCode.ValidationError, "Invalid FAQs JSON."));
                }
            }

            var page = await _context.ContentPages.FirstOrDefaultAsync(p => p.Slug == normalizedSlug);
            var now = DateTime.UtcNow;
            var userId = GetCurrentUserId();

            if (page == null)
            {
                page = new ContentPage
                {
                    Slug = normalizedSlug,
                    CreatedAt = now
                };
                _context.ContentPages.Add(page);
            }

            page.Title = request.Title.Trim();
            page.ContentHtml = string.IsNullOrWhiteSpace(request.ContentHtml) ? null : request.ContentHtml.Trim();
            page.ContentJson = string.IsNullOrWhiteSpace(request.ContentJson) ? null : request.ContentJson.Trim();
            page.IsPublished = request.IsPublished;
            page.UpdatedAt = now;
            page.UpdatedByUserId = userId;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(ToDto(page), "Content saved successfully."));
        }

        private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

        private int? GetCurrentUserId()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(userId, out var parsed) ? parsed : null;
        }

        private static object ToDto(ContentPage page) => new
        {
            page.Id,
            page.Slug,
            page.Title,
            page.ContentHtml,
            page.ContentJson,
            page.IsPublished,
            page.UpdatedAt,
            page.UpdatedByUserId
        };
    }
}
