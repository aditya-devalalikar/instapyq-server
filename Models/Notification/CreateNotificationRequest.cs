using System.ComponentModel.DataAnnotations;

public class CreateNotificationRequest
{
    public int? UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; }

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; }
}
