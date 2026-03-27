using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using pqy_server.Data;
using System.Data;
using System.Data.Common;

namespace pqy_server.Services
{
    public sealed class StreakAlertDispatcherHostedService : BackgroundService
    {
        private const long BackfillLockKey = 60327001;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly FcmNotificationService _fcm;
        private readonly AlertServiceOptions _options;
        private readonly ILogger<StreakAlertDispatcherHostedService> _logger;

        public StreakAlertDispatcherHostedService(
            IServiceScopeFactory scopeFactory,
            FcmNotificationService fcm,
            IOptions<AlertServiceOptions> options,
            ILogger<StreakAlertDispatcherHostedService> logger)
        {
            _scopeFactory = scopeFactory;
            _fcm = fcm;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("StreakAlertDispatcherHostedService started.");

            await TryBackfillMissingSchedulesAsync(stoppingToken);
            await ProcessDueAlertsAsync(stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, _options.PollIntervalSeconds)));

            try
            {
                while (await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await ProcessDueAlertsAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown.
            }

            _logger.LogInformation("StreakAlertDispatcherHostedService stopped.");
        }

        private async Task TryBackfillMissingSchedulesAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var scheduleService = scope.ServiceProvider.GetRequiredService<IStreakAlertScheduleService>();

            if (!await TryAcquireAdvisoryLockAsync(db, BackfillLockKey, ct))
                return;

            try
            {
                var backfilled = await scheduleService.BackfillMissingSchedulesAsync(ct);
                if (backfilled > 0)
                {
                    _logger.LogInformation(
                        "StreakAlertDispatcherHostedService: backfilled schedules for {Count} streak(s) after startup.",
                        backfilled);
                }
            }
            finally
            {
                await ReleaseAdvisoryLockAsync(db, BackfillLockKey, ct);
            }
        }

        private async Task ProcessDueAlertsAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var nowUtc = DateTime.UtcNow;
                var claimedIds = await ClaimDueScheduleIdsAsync(db, nowUtc, ct);

                if (claimedIds.Count == 0)
                    return;

                var workItems = await db.StreakAlertSchedules
                    .AsNoTracking()
                    .Where(s => claimedIds.Contains(s.Id))
                    .Select(s => new AlertWorkItem
                    {
                        Id = s.Id,
                        StreakId = s.StreakId,
                        StreakName = s.Streak.Name,
                        UserId = s.UserId,
                        FcmToken = s.User.FcmToken,
                        Timezone = s.Timezone,
                        LocalHour = s.LocalHour,
                        LocalMinute = s.LocalMinute,
                        AttemptCount = s.AttemptCount
                    })
                    .ToListAsync(ct);

                var sent = 0;
                var skipped = 0;
                var failed = 0;

                await Parallel.ForEachAsync(
                    workItems,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Math.Max(1, _options.MaxConcurrentSends),
                        CancellationToken = ct
                    },
                    async (item, token) =>
                    {
                        var result = await ProcessWorkItemAsync(item, token);
                        switch (result)
                        {
                            case DeliveryResult.Sent:
                                Interlocked.Increment(ref sent);
                                break;
                            case DeliveryResult.Skipped:
                                Interlocked.Increment(ref skipped);
                                break;
                            default:
                                Interlocked.Increment(ref failed);
                                break;
                        }
                    });

                _logger.LogInformation(
                    "StreakAlertDispatcherHostedService: claimed {Claimed} due alert(s); sent={Sent}, skipped={Skipped}, failed={Failed}.",
                    claimedIds.Count, sent, skipped, failed);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "StreakAlertDispatcherHostedService: unhandled error while processing due alerts.");
            }
        }

        private async Task<DeliveryResult> ProcessWorkItemAsync(AlertWorkItem item, CancellationToken ct)
        {
            var utcNow = DateTime.UtcNow;
            var nextRegularFireUtc = StreakAlertTimeCalculator.ComputeNextFireUtc(
                item.Timezone,
                item.LocalHour,
                item.LocalMinute,
                utcNow.AddSeconds(1));

            if (string.IsNullOrWhiteSpace(item.FcmToken))
            {
                await ReleaseToNextOccurrenceAsync(item.Id, nextRegularFireUtc, utcNow, ct);
                return DeliveryResult.Skipped;
            }

            try
            {
                await _fcm.SendNotificationAsync(
                    item.FcmToken,
                    $"Time for {item.StreakName}!",
                    "Don't break the chain! Keep your streak burning.");

                var localDate = StreakAlertTimeCalculator.GetLocalDate(utcNow, item.Timezone);
                await MarkSentAsync(item.Id, utcNow, localDate, nextRegularFireUtc, ct);
                return DeliveryResult.Sent;
            }
            catch (Exception ex)
            {
                var nextAttemptCount = item.AttemptCount + 1;
                var errorMessage = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;

                if (nextAttemptCount >= Math.Max(1, _options.MaxRetryAttempts))
                {
                    await SkipFailedOccurrenceAsync(item.Id, utcNow, nextRegularFireUtc, errorMessage, ct);
                }
                else
                {
                    await ScheduleRetryAsync(
                        item.Id,
                        utcNow.AddSeconds(Math.Max(15, _options.RetryDelaySeconds)),
                        utcNow,
                        nextAttemptCount,
                        errorMessage,
                        ct);
                }

                _logger.LogWarning(ex,
                    "StreakAlertDispatcherHostedService: FCM send failed for schedule {ScheduleId} (streak {StreakId}, user {UserId}).",
                    item.Id, item.StreakId, item.UserId);

                return DeliveryResult.Failed;
            }
        }

        private async Task<List<long>> ClaimDueScheduleIdsAsync(AppDbContext db, DateTime utcNow, CancellationToken ct)
        {
            var leaseUntilUtc = utcNow.AddSeconds(Math.Max(15, _options.LeaseSeconds));
            var claimedIds = new List<long>();
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
                await connection.OpenAsync(ct);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    WITH due AS (
                        SELECT "Id"
                        FROM "StreakAlertSchedules"
                        WHERE "IsActive" = TRUE
                          AND "NextFireUtc" <= @nowUtc
                          AND ("LeaseUntilUtc" IS NULL OR "LeaseUntilUtc" < @nowUtc)
                        ORDER BY "NextFireUtc"
                        LIMIT @batchSize
                        FOR UPDATE SKIP LOCKED
                    )
                    UPDATE "StreakAlertSchedules" AS schedule
                    SET "LeaseUntilUtc" = @leaseUntilUtc,
                        "UpdatedAt" = @nowUtc
                    FROM due
                    WHERE schedule."Id" = due."Id"
                    RETURNING schedule."Id";
                    """;

                command.Parameters.Add(CreateParameter(command, "@nowUtc", utcNow));
                command.Parameters.Add(CreateParameter(command, "@leaseUntilUtc", leaseUntilUtc));
                command.Parameters.Add(CreateParameter(command, "@batchSize", Math.Max(1, _options.BatchSize)));

                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    claimedIds.Add(reader.GetInt64(0));
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }

            return claimedIds;
        }

        private async Task<bool> TryAcquireAdvisoryLockAsync(AppDbContext db, long lockKey, CancellationToken ct)
        {
            var result = await ExecuteScalarAsync(db, "SELECT pg_try_advisory_lock(@lockKey)", lockKey, ct);
            return result is bool acquired && acquired;
        }

        private Task ReleaseAdvisoryLockAsync(AppDbContext db, long lockKey, CancellationToken ct) =>
            ExecuteScalarAsync(db, "SELECT pg_advisory_unlock(@lockKey)", lockKey, ct);

        private static async Task<object?> ExecuteScalarAsync(AppDbContext db, string sql, long lockKey, CancellationToken ct)
        {
            var connection = db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
                await connection.OpenAsync(ct);

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.Add(CreateParameter(command, "@lockKey", lockKey));
                return await command.ExecuteScalarAsync(ct);
            }
            finally
            {
                if (shouldClose)
                    await connection.CloseAsync();
            }
        }

        private async Task MarkSentAsync(
            long scheduleId,
            DateTime sentAtUtc,
            DateOnly localDate,
            DateTime nextFireUtc,
            CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.StreakAlertSchedules
                .Where(s => s.Id == scheduleId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.LastSentUtc, sentAtUtc)
                    .SetProperty(s => s.LastSentLocalDate, localDate)
                    .SetProperty(s => s.NextFireUtc, nextFireUtc)
                    .SetProperty(s => s.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(s => s.AttemptCount, 0)
                    .SetProperty(s => s.LastError, (string?)null)
                    .SetProperty(s => s.UpdatedAt, sentAtUtc), ct);
        }

        private async Task ScheduleRetryAsync(
            long scheduleId,
            DateTime retryAtUtc,
            DateTime updatedAtUtc,
            int attemptCount,
            string errorMessage,
            CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.StreakAlertSchedules
                .Where(s => s.Id == scheduleId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.NextFireUtc, retryAtUtc)
                    .SetProperty(s => s.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(s => s.AttemptCount, attemptCount)
                    .SetProperty(s => s.LastError, errorMessage)
                    .SetProperty(s => s.UpdatedAt, updatedAtUtc), ct);
        }

        private async Task SkipFailedOccurrenceAsync(
            long scheduleId,
            DateTime updatedAtUtc,
            DateTime nextFireUtc,
            string errorMessage,
            CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.StreakAlertSchedules
                .Where(s => s.Id == scheduleId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.NextFireUtc, nextFireUtc)
                    .SetProperty(s => s.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(s => s.AttemptCount, 0)
                    .SetProperty(s => s.LastError, errorMessage)
                    .SetProperty(s => s.UpdatedAt, updatedAtUtc), ct);
        }

        private async Task ReleaseToNextOccurrenceAsync(
            long scheduleId,
            DateTime nextFireUtc,
            DateTime updatedAtUtc,
            CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            await db.StreakAlertSchedules
                .Where(s => s.Id == scheduleId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.NextFireUtc, nextFireUtc)
                    .SetProperty(s => s.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(s => s.AttemptCount, 0)
                    .SetProperty(s => s.LastError, (string?)null)
                    .SetProperty(s => s.UpdatedAt, updatedAtUtc), ct);
        }

        private static DbParameter CreateParameter(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            return parameter;
        }

        private sealed class AlertWorkItem
        {
            public long Id { get; init; }
            public int StreakId { get; init; }
            public string StreakName { get; init; } = string.Empty;
            public int UserId { get; init; }
            public string? FcmToken { get; init; }
            public string Timezone { get; init; } = StreakAlertTimeCalculator.DefaultTimezone;
            public int LocalHour { get; init; }
            public int LocalMinute { get; init; }
            public int AttemptCount { get; init; }
        }

        private enum DeliveryResult
        {
            Sent,
            Skipped,
            Failed
        }
    }
}
