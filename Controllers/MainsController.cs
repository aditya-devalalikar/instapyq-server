using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pqy_server.Constants;
using pqy_server.Data;
using pqy_server.Models.Mains;
using pqy_server.Shared;

namespace pqy_server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MainsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MainsController(AppDbContext context)
        {
            _context = context;
        }

        // 📥 GET: /api/mains/all
        // Returns all mains questions with optional filters
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] int? yearId,
            [FromQuery] PaperType? paperType,
            [FromQuery] int? paperNumber,
            [FromQuery] OptionalSubject? optionalSubject,
            [FromQuery] int? subjectId,
            [FromQuery] int? topicId)
        {
            var query = _context.MainsQuestions
                .Include(m => m.Year)
                .Include(m => m.Topic)
                .AsQueryable();

            if (yearId.HasValue)
                query = query.Where(m => m.YearId == yearId.Value);

            if (paperType.HasValue)
                query = query.Where(m => m.PaperType == paperType.Value);

            if (paperNumber.HasValue)
                query = query.Where(m => m.PaperNumber == paperNumber.Value);

            if (optionalSubject.HasValue)
                query = query.Where(m => m.OptionalSubject == optionalSubject.Value);

            if (subjectId.HasValue)
                query = query.Where(m => m.Topic != null && m.Topic.SubjectId == subjectId.Value);

            if (topicId.HasValue)
                query = query.Where(m => m.TopicId == topicId.Value);

            var questions = await query
                .OrderBy(m => m.PaperType)
                .ThenBy(m => m.PaperNumber)
                .ThenBy(m => m.QuestionNumber)
                .Select(m => new MainsQuestionDto
                {
                    Id = m.Id,
                    Year = m.Year.YearName,
                    YearId = m.YearId,
                    PaperType = m.PaperType,
                    PaperNumber = m.PaperNumber,
                    OptionalSubject = m.OptionalSubject.ToString(),
                    Section = m.Section,
                    QuestionNumber = m.QuestionNumber,
                    QuestionText = m.QuestionText,
                    Marks = m.Marks,
                    Topic = m.Topic != null ? m.Topic.TopicName : null,
                    Subject = m.Topic != null ? m.Topic.SubjectName : null,
                    SubjectId = m.Topic != null ? m.Topic.SubjectId : null,
                    TopicId = m.TopicId,
                })
                .ToListAsync();
            return Ok(ApiResponse<object>.Success(questions));
        }

        // 📥 GET: /api/mains/{id}
        // Get single mains question by ID
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var question = await _context.MainsQuestions
                .Include(m => m.Year)
                .Include(m => m.Topic)
                .Where(m => m.Id == id)
                .Select(m => new MainsQuestionDto
                {
                    Id = m.Id,
                    Year = m.Year.YearName,
                    YearId = m.YearId,
                    PaperType = m.PaperType,
                    PaperNumber = m.PaperNumber,
                    OptionalSubject = m.OptionalSubject.ToString(),
                    Section = m.Section,
                    QuestionNumber = m.QuestionNumber,
                    QuestionText = m.QuestionText,
                    Marks = m.Marks,
                    Topic = m.Topic != null ? m.Topic.TopicName : null,
                    Subject = m.Topic != null ? m.Topic.SubjectName : null,
                    SubjectId = m.Topic != null ? m.Topic.SubjectId : null,
                    TopicId = m.TopicId,
                })
                .FirstOrDefaultAsync();

            if (question == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found"));

            return Ok(ApiResponse<object>.Success(question));
        }

        // ➕ POST: /api/mains
        // ➕ POST: /api/mains
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateMainsQuestionDto dto)
        {
            var question = new MainsQuestion
            {
                YearId = dto.YearId,
                PaperType = dto.PaperType,
                PaperNumber = dto.PaperNumber,
                OptionalSubject = dto.OptionalSubject,
                Section = dto.Section,
                QuestionNumber = dto.QuestionNumber,
                QuestionText = dto.QuestionText,
                Marks = dto.Marks,
                TopicId = dto.TopicId,        // ✅ can be null
                SubjectId = dto.SubjectId     // ✅ can be null
            };

            _context.MainsQuestions.Add(question);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Mains question created successfully."));
        }

        // ✏️ PUT: /api/mains/{id}
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateMainsQuestionDto updatedQuestion)
        {
            var question = await _context.MainsQuestions.FindAsync(id);
            if (question == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found"));

            question.YearId = updatedQuestion.YearId;
            question.PaperType = updatedQuestion.PaperType;
            question.PaperNumber = updatedQuestion.PaperNumber;
            question.OptionalSubject = updatedQuestion.OptionalSubject;
            question.Section = updatedQuestion.Section;
            question.QuestionNumber = updatedQuestion.QuestionNumber;
            question.QuestionText = updatedQuestion.QuestionText;
            question.Marks = updatedQuestion.Marks;
            question.TopicId = updatedQuestion.TopicId;   // ✅ can be null
            question.SubjectId = updatedQuestion.SubjectId; // ✅ can be null
            question.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(ApiResponse<object>.Success(question, "Mains question updated."));
        }

        // ❌ DELETE: /api/mains/{id}
        [Authorize(Roles = RoleConstant.Admin)]
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var question = await _context.MainsQuestions.FindAsync(id);
            if (question == null)
                return NotFound(ApiResponse<string>.Failure(ResultCode.NotFound, "Question not found"));

            _context.MainsQuestions.Remove(question);
            await _context.SaveChangesAsync();

            return Ok(ApiResponse<string>.Success("Mains question deleted."));
        }
    }
}
