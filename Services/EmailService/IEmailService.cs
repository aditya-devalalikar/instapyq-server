using System;
using System.Threading.Tasks;

namespace pqy_server.Services.EmailService
{
    public interface IEmailService
    {
        Task SendWelcomeEmail(string toEmail, string userName);

        Task SendPaymentSuccessEmail(
            string toEmail,
            string userName,
            decimal amount,
            DateTime expiryDate);

        Task SendPaymentFailureEmail(
            string toEmail,
            string userName);

        Task SendCampaignEmail(
            string toEmail,
            string templateKey,
            object mergeData);

        Task SendReportReceivedEmail(
            string toEmail,
            string userName,
            int questionId);

        Task SendReportResolvedEmail(
            string toEmail,
            string userName,
            int questionId);

        Task SendOtpEmail(string toEmail, string otp);
    }
}
