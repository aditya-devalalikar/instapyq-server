using pqy_server.Models.Logs;

namespace pqy_server.Services
{
    public interface ILogService
    {
        Task LogAsync(Log log);
    }
}
