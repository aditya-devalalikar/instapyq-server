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
                },
                // Android: route to the channel that has the custom sound registered
                Android = new AndroidConfig
                {
                    Notification = new AndroidNotification
                    {
                        ChannelId = "streak-alerts-v2",
                    }
                },
                // iOS: play the custom sound bundled in the app
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "notification_tone.mp3",
                    }
                }
            };

            return await FirebaseMessaging.DefaultInstance.SendAsync(message);
        }
    }
}
