using FirebaseAdmin.Messaging;

namespace pqy_server.Services
{
    public class FcmNotificationService
    {
        public async Task<string> SendNotificationAsync(string token, string title, string body)
        {
            var message = new Message()
            {
                Token = token,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                }
            };

            return await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
    }
}
