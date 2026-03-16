using System.ComponentModel.DataAnnotations;

public class ReportQuestionRequest
{
    public int QuestionId { get; set; }

    public bool WrongAnswer { get; set; } = false;
    public bool WrongExplanation { get; set; } = false;
    public bool WrongOptions { get; set; } = false;
    public bool QuestionFormatting { get; set; } = false;
    public bool DuplicateQuestion { get; set; } = false;
    public bool Other { get; set; } = false;

    [MaxLength(500)]
    public string? OtherDetails { get; set; }
}
