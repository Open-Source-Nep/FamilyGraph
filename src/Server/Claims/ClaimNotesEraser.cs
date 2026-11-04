using FamilyGraph.Application.Claims;

namespace Server.Claims;

/// <summary>
/// Erases the notes of claims closed more than <c>Claims:EvidenceRetentionDays</c> ago (AC-04.10,
/// D-052): a minute after startup, then once a day. A failure is logged and retried at the next run.
/// </summary>
internal sealed partial class ClaimNotesEraser(IServiceScopeFactory scopes, TimeProvider time, ILogger<ClaimNotesEraser> logger)
    : BackgroundService
{
    private static readonly TimeSpan FirstRun = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstRun, time, stoppingToken);
            using var timer = new PeriodicTimer(Interval, time);
            do
            {
                await EraseAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task EraseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var erased = await scope.ServiceProvider.GetRequiredService<ClaimService>().EraseExpiredNotesAsync(cancellationToken);
            if (erased > 0)
            {
                LogErased(erased);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            LogFailed(e);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Erased the notes of {Count} closed claims.")]
    private partial void LogErased(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Erasing old claim notes failed; retrying at the next run.")]
    private partial void LogFailed(Exception exception);
}
