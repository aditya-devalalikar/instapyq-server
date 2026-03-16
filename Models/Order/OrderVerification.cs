namespace pqy_server.Models.Order
{
    public class OrderVerification
    {
        public string OrderId { get; set; } = string.Empty;
        public string PaymentId { get; set; } = string.Empty;
        public string Signature { get; set; } = string.Empty;
    }
}
