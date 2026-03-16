using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserEntity = pqy_server.Models.Users.User;
using pqy_server.Models.Questions;

namespace pqy_server.Models.QuestionNotes;

public class QuestionNote
{
    [Key]
    public int NoteId { get; set; }

    public int UserId { get; set; }
    public int QuestionId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("UserId")]
    public UserEntity User { get; set; }

    [ForeignKey("QuestionId")]
    public Question? Question { get; set; }
}