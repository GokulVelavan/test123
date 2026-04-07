using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Worker.Options;
using DRS.Scaffold.Syslog.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DRS.Scaffold.Syslog.Worker.Workers;

/// <summary>
/// Dispatches notifications for unprocessed alert events.
/// Looks up <c>alert_notification</c> join rows to find which channels
/// are linked to the fired rule, then sends via <see cref="NotificationSender"/>.
/// Falls back to ALL enabled channels when no rule-specific mapping exists.
/// </summary>
public sealed class AlertNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory              _scopeFactory;
    private readonly NotificationSender                _sender;
    private readonly WorkerOptions                     _opts;
    private readonly ILogger<AlertNotificationWorker>  _logger;

    public AlertNotificationWorker(
        IServiceScopeFactory             scopeFactory,
        NotificationSender               sender,
        IOptions<WorkerOptions>          options,
        ILogger<AlertNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _sender       = sender;
        _opts         = options.Value;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "AlertNotificationWorker starting. Interval={Int}s",
            _opts.NotificationIntervalSeconds);

        var interval = TimeSpan.FromSeconds(_opts.NotificationIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AlertNotificationWorker error -- will retry next cycle");
            }

            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("AlertNotificationWorker stopped.");
    }

    private async Task DispatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        // Grab unnotified alerts (batch of 100)
        var pending = await db.AlertEvents
            .Where(a => !a.IsDeleted && a.NotifiedAt == null)
            .OrderBy(a => a.Id)
            .Take(100)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        // Load enabled channels and rule->channel mappings
        var channels = await db.NotificationChannels
            .Where(c => !c.IsDeleted && c.Enabled)
            .ToListAsync(ct);

        if (channels.Count == 0)
        {
            // No channels configured -- mark as notified so we don't spin forever
            foreach (var a in pending)
                a.NotifiedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        var mappings = await db.Set<AlertNotification>()
            .Where(m => !m.IsDeleted)
            .ToListAsync(ct);

        int notified = 0;
        int failed   = 0;

        foreach (var alert in pending)
        {
            // Find channels linked to this alert's rule
            var ruleChannelIds = mappings
                .Where(m => m.RuleId == alert.RuleId)
                .Select(m => m.NotificationId)
                .ToHashSet();

            // If no specific mapping, fall back to all enabled channels
            var targets = ruleChannelIds.Count > 0
                ? channels.Where(c => ruleChannelIds.Contains(c.Id)).ToList()
                : channels;

            bool anyFailed = false;
            foreach (var ch in targets)
            {
                bool sent = await _sender.SendAsync(ch, alert, ct);
                if (!sent) anyFailed = true;
            }

            if (anyFailed)
            {
                // At least one channel failed — leave NotifiedAt null so the
                // worker retries this alert on the next cycle
                failed++;
                _logger.LogWarning(
                    "One or more channels failed for alert id={Id} (rule={RuleId}) -- will retry",
                    alert.Id, alert.RuleId);
            }
            else
            {
                alert.NotifiedAt = DateTimeOffset.UtcNow;
                alert.UpdatedAt  = DateTimeOffset.UtcNow;
                alert.UpdatedBy  = _opts.ServiceAccount;
                notified++;
            }
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Notification cycle: {Notified} dispatched, {Failed} pending retry",
            notified, failed);
    }
}
