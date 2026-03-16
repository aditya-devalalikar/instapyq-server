using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace pqy_server.Models.Progress
{
    public class ExamProgress
    {
        [Key]
        public string ExamProgressId { get; set; }

        public int UserId { get; set; }
        public string ModeType { get; set; } // "Year", "Subject", "Topic"
        public int? YearId { get; set; }

        // For now keeping CSV (we’ll normalize later if needed)
        public string SubjectIds { get; set; }
        public string TopicIds { get; set; }

        public int QuestionCount { get; set; }
        public int AttemptedCount { get; set; }
        public int CorrectCount { get; set; }
        public int WrongCount { get; set; }
        public int SkippedCount { get; set; }

        public int Elim1 { get; set; }
        public int Elim1Correct { get; set; }
        public int Elim1Wrong { get; set; }
        public int Elim2 { get; set; }
        public int Elim2Correct { get; set; }
        public int Elim2Wrong { get; set; }
        public int Elim3Correct { get; set; }
        public int Elim3 { get; set; }
        public int Elim3Wrong { get; set; }

        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
