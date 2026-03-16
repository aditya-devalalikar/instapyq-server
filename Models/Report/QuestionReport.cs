using pqy_server.Models.Questions;
using pqy_server.Models.Users;

public class QuestionReport
{
    public int Id { get; set; }

    public int QuestionId { get; set; }
    public Question? Question { get; set; }

    public int UserId { get; set; }
    public User User { get; set; }

    // Default all to false
    public bool WrongAnswer { get; set; } = false;
    public bool WrongExplanation { get; set; } = false;
    public bool WrongOptions { get; set; } = false;
    public bool QuestionFormatting { get; set; } = false;
    public bool DuplicateQuestion { get; set; } = false;
    public bool Other { get; set; } = false;

    public string? OtherDetails { get; set; }

    public bool IsResolved { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
