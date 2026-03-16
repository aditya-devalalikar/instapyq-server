namespace pqy_server.Models.Order
{
    public class Plan
    {
        public int Id { get; set; }

        // Label for UI
        public string DurationLabel { get; set; } = string.Empty; // e.g., "1 Month", "3 Months"

        // Pricing
        public int Amount { get; set; }         // Actual price user pays
        public int OriginalAmount { get; set; } // Original price for strike-through UI

        // Duration in days (used to calculate order expiry)
        public int DurationInDays { get; set; }

        // Whether this plan is recommended
        public bool IsRecommended { get; set; }
    }
}
