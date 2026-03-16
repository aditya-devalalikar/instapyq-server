using Newtonsoft.Json;

namespace pqy_server.Models.Orders
{
    // Top-level webhook payload
    public class RazorpayWebhookPayload
    {
        [JsonProperty("event")]
        public string Event { get; set; } = string.Empty;

        [JsonProperty("payload")]
        public RazorpayWebhookEventPayload Payload { get; set; } = new();

        [JsonProperty("created_at")]
        public long CreatedAt { get; set; }
    }

    // Wrapper for payment + refund (Razorpay sends different shapes per event)
    public class RazorpayWebhookEventPayload
    {
        [JsonProperty("payment")]
        public RazorpayPaymentWrapper? Payment { get; set; }

        [JsonProperty("refund")]
        public RazorpayRefundWrapper? Refund { get; set; }
    }

    // Payment wrapper (because Razorpay nests entity)
    public class RazorpayPaymentWrapper
    {
        [JsonProperty("entity")]
        public RazorpayPaymentEntity Entity { get; set; } = new();
    }

    // Refund wrapper (Razorpay nests refund entity under payload.refund.entity)
    public class RazorpayRefundWrapper
    {
        [JsonProperty("entity")]
        public RazorpayRefundEntity Entity { get; set; } = new();
    }

    // Actual payment info
    public class RazorpayPaymentEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("amount")]
        public int Amount { get; set; } // in paise

        [JsonProperty("currency")]
        public string Currency { get; set; } = "INR";

        [JsonProperty("order_id")]
        public string Order_Id { get; set; } = string.Empty;

        [JsonProperty("captured")]
        public bool Captured { get; set; }
    }

    // Actual refund info
    public class RazorpayRefundEntity
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("payment_id")]
        public string PaymentId { get; set; } = string.Empty;

        [JsonProperty("amount")]
        public int Amount { get; set; } // in paise

        [JsonProperty("currency")]
        public string Currency { get; set; } = "INR";

        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        // The order_id is nested inside the payment object within refund webhooks,
        // but we also look it up via the payment_id → order mapping in the DB
    }
}

