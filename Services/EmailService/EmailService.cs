using pqy_server.Models.Email;
using pqy_server.Services.EmailService;
using System.Text;
using System.Text.Json;
using Serilog;

public class EmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<EmailService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    private async Task SendTemplateEmail(
    string toEmail,
    string templateKey,
    object mergeInfo)
    {
        var apiKey = _config["ZeptoMail:ApiKey"];
        var fromEmail = _config["ZeptoMail:FromEmail"];
        var fromName = _config["ZeptoMail:FromName"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new Exception("ZeptoMail ApiKey missing.");

        if (string.IsNullOrWhiteSpace(templateKey))
            throw new Exception("ZeptoMail TemplateKey missing.");

        var requestBody = new
        {
            template_key = templateKey,
            from = new
            {
                address = fromEmail,
                name = fromName
            },
            to = new[]
            {
            new
            {
                email_address = new
                {
                    address = toEmail
                }
            }
        },
            merge_info = mergeInfo   // ✅ ROOT LEVEL (IMPORTANT)
        };

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.zeptomail.in/v1.1/email/template");

        request.Headers.Add("Authorization", $"Zoho-enczapikey {apiKey}");

        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.SendAsync(request);

        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Log.Error("ZeptoMail error. eml={eml}, res={res}", toEmail, responseContent);
            throw new Exception($"ZeptoMail error: {responseContent}");
        }

        // Space optimization: only log success for critical payments, skip routine OTP/Welcome success logs
    }

    public async Task SendWelcomeEmail(string toEmail, string userName)
    {
        var templateKey = _config["ZeptoMail:WelcomeTemplateKey"];
        _logger.LogInformation("TemplateKey from config: {Key}", templateKey);
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            _logger.LogError("ZeptoMail WelcomeTemplateKey is missing in configuration.");
            throw new Exception("ZeptoMail WelcomeTemplateKey is missing in configuration.");
        }

        _logger.LogInformation(
            "Preparing to send Welcome Email. To: {Email}, User: {UserName}, TemplateKey: {TemplateKey}",
            toEmail,
            userName,
            templateKey
        );

        try
        {
            await SendTemplateEmail(
                toEmail,
                templateKey,
                new
                {
                    user_name = userName
                });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Welcome email failed. eml={eml}", toEmail);
            throw; // important so controller knows it failed
        }
    }


    public async Task SendPaymentSuccessEmail(
    string toEmail,
    string userName,
    decimal amount,
    DateTime expiryDate)
    {
        var templateKey = _config["ZeptoMail:PaymentSuccessTemplateKey"];

        if (string.IsNullOrWhiteSpace(templateKey))
        {
            _logger.LogError("PaymentSuccessTemplateKey missing in configuration.");
            return;
        }

        _logger.LogInformation(
            "Preparing Payment Success Email. To: {Email}, Amount: {Amount}",
            toEmail,
            amount);

        await SendTemplateEmail(
            toEmail,
            templateKey,
            new
            {
                user_name = userName,
                amount = amount.ToString("0.00"),
                expiry_date = expiryDate.ToString("dd MMM yyyy")
            });
    }


    public async Task SendPaymentFailureEmail(
    string toEmail,
    string userName)
    {
        var templateKey = _config["ZeptoMail:PaymentFailureTemplateKey"];

        if (string.IsNullOrWhiteSpace(templateKey))
        {
            Log.Error("Email error: PaymentFailureTemplateKey missing.");
            return;
        }

        await SendTemplateEmail(
            toEmail,
            templateKey,
            new
            {
                user_name = userName
            });
    }


    public async Task SendReportReceivedEmail(
    string toEmail,
    string userName,
    int questionId)
    {
        var templateKey = _config["ZeptoMail:ReportReceivedTemplateKey"];

        if (string.IsNullOrWhiteSpace(templateKey))
        {
            Log.Error("Email error: ReportReceivedTemplateKey missing.");
            return;
        }

        await SendTemplateEmail(
            toEmail,
            templateKey,
            new
            {
                user_name = userName,
                question_id = questionId
            });
    }


    public async Task SendReportResolvedEmail(
    string toEmail,
    string userName,
    int questionId)
    {
        var templateKey = _config["ZeptoMail:ReportResolvedTemplateKey"];

        if (string.IsNullOrWhiteSpace(templateKey))
        {
            Log.Error("Email error: ReportResolvedTemplateKey missing.");
            return;
        }

        await SendTemplateEmail(
            toEmail,
            templateKey,
            new
            {
                user_name = userName,
                question_id = questionId
            });
    }


    public async Task SendCampaignEmail(
        string toEmail,
        string templateKey,
        object mergeData)
    {
        await SendTemplateEmail(
            toEmail,
            templateKey,
            mergeData);
    }

    public async Task SendOtpEmail(string toEmail, string otp)
    {
        var templateKey = _config["ZeptoMail:OtpTemplateKey"];

        if (string.IsNullOrWhiteSpace(templateKey))
            throw new Exception("ZeptoMail OtpTemplateKey missing in configuration.");

        await SendTemplateEmail(toEmail, templateKey, new { otp_code = otp });
    }
}
