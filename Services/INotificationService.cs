namespace pqy_server.Services
{
    public interface INotificationService
    {
        Task SendNotificationToUserAsync(int userId, string title, string message);
    }
}
