using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using pqy_server.Models.Order;
using UserEntity = pqy_server.Models.Users.User;

namespace pqy_server.Models.Orders
{
    public class Order
    {
        public int Id { get; set; }

        // FK
        public int UserId { get; set; }
        public UserEntity User { get; set; }   // ✅ Navigation

        // Plan reference
        public int PlanId { get; set; }

        // Razorpay IDs
        public string RazorpayOrderId { get; set; } = string.Empty;
        public string? PaymentId { get; set; }

        // Money
        public int Amount { get; set; }       // in ₹
        public int? AmountPaid { get; set; }  // in ₹

        // Meta
        public string Currency { get; set; } = "INR";

        // Order lifecycle — stored as string in DB via StringEnumConverter
        [JsonConverter(typeof(StringEnumConverter))]
        public OrderStatus Status { get; set; } = OrderStatus.Created;
        // Created | Paid | Failed | Refunded | Cancelled

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // ✅ Track order completion time
        public DateTime? CompletedAt { get; set; } // Set when Paid, Failed, Refunded, or Cancelled

        // ✅ Track when order expires
        public DateTime? ExpiresAt { get; set; }

        // Helper property (not stored in DB) to calculate remaining days
        public int? RemainingDays => ExpiresAt.HasValue
            ? (ExpiresAt.Value - DateTime.UtcNow).Days
            : null;
    }
}

