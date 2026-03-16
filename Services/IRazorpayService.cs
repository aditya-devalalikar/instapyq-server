using Razorpay.Api;
using pqy_server.Models.Order;

namespace pqy_server.Services
{
    public interface IRazorpayService
    {
        string CreateOrder(decimal amount, string receipt);
    }

    public class RazorpayService : IRazorpayService
    {
        private readonly RazorpaySettings _settings;

        public RazorpayService(Microsoft.Extensions.Options.IOptions<pqy_server.Models.Order.RazorpaySettings> options)
        {
            _settings = options.Value;
        }

        public string CreateOrder(decimal amount, string receipt)
        {
            var client = new RazorpayClient(_settings.KeyId, _settings.KeySecret);
            
            var options = new Dictionary<string, object>
            {
                { "amount", (int)(amount * 100) },
                { "currency", "INR" },
                { "receipt", receipt },
                { "payment_capture", 1 }
            };

            Order order = client.Order.Create(options);
            return order["id"].ToString();
        }
    }
}
