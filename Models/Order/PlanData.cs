namespace pqy_server.Models.Order
{
    public static class PlanData
    {
        public static readonly List<Plan> DefaultPlans = new List<Plan>
        {
            new Plan { Id = 1, DurationLabel = "1 Month", Amount = 199, OriginalAmount = 299, DurationInDays = 30, IsRecommended = false },
            new Plan { Id = 2, DurationLabel = "3 Months", Amount = 499, OriginalAmount = 699, DurationInDays = 90, IsRecommended = true },
            new Plan { Id = 3, DurationLabel = "6 Months", Amount = 899, OriginalAmount = 1299, DurationInDays = 180, IsRecommended = false },
            new Plan { Id = 4, DurationLabel = "12 Months", Amount = 1499, OriginalAmount = 1999, DurationInDays = 365, IsRecommended = false },
        };
    }
}
