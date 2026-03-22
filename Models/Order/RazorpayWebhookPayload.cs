using System.Text.Json.Serialization;

namespace pqy_server.Models.Orders
{
    // Top-level webhook payload
    public class RazorpayWebhookPayload
    {
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("payload")]
        public RazorpayWebhookEventPayload Payload { get; set; } = new();

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }
    }

    // Wrapper for payment + refund (Razorpay sends different shapes per event)
    public class RazorpayWebhookEventPayload
    {
        [JsonPropertyName("payment")]
        public RazorpayPaymentWrapper? Payment { get; set; }

        [JsonPropertyName("refund")]
        public RazorpayRefundWrapper? Refund { get; set; }
    }

    // Payment wrapper (because Razorpay nests entity)
    public class RazorpayPaymentWrapper
    {
        [JsonPropertyName("entity")]
        public RazorpayPaymentEntity Entity { get; set; } = new();
    }

    // Refund wrapper (Razorpay nests refund entity under payload.refund.entity)
    public class RazorpayRefundWrapper
    {
        [JsonPropertyName("entity")]
        public RazorpayRefundEntity Entity { get; set; } = new();
    }

    // Actual payment info
    public class RazorpayPaymentEntity
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public int Amount { get; set; } // in paise

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "INR";

        [JsonPropertyName("order_id")]
        public string Order_Id { get; set; } = string.Empty;

        [JsonPropertyName("captured")]
        public bool Captured { get; set; }
    }

    // Actual refund info
    public class RazorpayRefundEntity
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("payment_id")]
        public string PaymentId { get; set; } = string.Empty;

        [JsonPropertyName("amount")]
        public int Amount { get; set; } // in paise

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "INR";

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        // The order_id is nested inside the payment object within refund webhooks,
        // but we also look it up via the payment_id → order mapping in the DB
    }
}

