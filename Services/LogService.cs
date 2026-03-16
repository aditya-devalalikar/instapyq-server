using LogModel = pqy_server.Models.Logs.Log;
using Serilog;

namespace pqy_server.Services
{
    public class LogService : ILogService
    {
        public Task LogAsync(LogModel log)
        {
            Log.ForContext("uid", log.UserId)
               .ForContext("role", log.Role)
               .ForContext("prm", log.IsPremium)
               .ForContext("path", log.RequestPath)
               .ForContext("mtd", log.HttpMethod)
               .Error("{Message} | Exception: {Exception}",
                      log.Message,
                      log.Exception);

            return Task.CompletedTask;
        }
    }
}
