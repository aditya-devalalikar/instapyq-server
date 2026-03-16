namespace pqy_server.Models.Logs
{
    public class Log
    {
        public int Id { get; set; }

        // Core logging info
        public string LogLevel { get; set; } = "Information";
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }

        // User info
        public int? UserId { get; set; } // nullable → anonymous allowed
        public string? Role { get; set; }
        public bool? IsPremium { get; set; }

        // Request context
        public string? RequestPath { get; set; }
        public string? HttpMethod { get; set; }

        // Timestamp
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
