namespace pqy_server.Models.Order
{
    public class MyPlanDto
    {
        public bool IsPremium { get; set; }

        public DateTime? ExpiresAt { get; set; }

        public int? RemainingDays { get; set; }

        public string? OrderId { get; set; }

        public decimal? AmountPaid { get; set; }
    }
}
