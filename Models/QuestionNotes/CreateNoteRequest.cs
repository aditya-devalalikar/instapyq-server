using System.ComponentModel.DataAnnotations;

namespace pqy_server.DTOs.QuestionNotes;

public class CreateNoteRequest
{
    [Required]
    [MaxLength(5000)]
    public string Content { get; set; }
}
