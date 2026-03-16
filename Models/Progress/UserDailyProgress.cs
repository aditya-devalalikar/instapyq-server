namespace pqy_server.Models.Progress
{
    public class UserDailyProgress
    {
        public int UserId { get; set; }
        public DateOnly Date { get; set; }
        public int SubjectId { get; set; }
        public int ExamId { get; set; }
        public int Attempts { get; set; }
        public int Correct { get; set; }
    }
}
