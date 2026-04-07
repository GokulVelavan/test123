using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Models;
using DRS.Scaffold.Syslog.Worker.Extensions;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Worker.Services;

/// <summary>
/// Dispatches alert notifications to configured channels (Email only).
/// Each method is fire-and-forget safe -- exceptions are caught and logged.
/// </summary>
public sealed class NotificationSender
{
    private readonly IHttpClientFactory               _httpFactory;
    private readonly ILogger<NotificationSender>      _logger;
    private readonly IServiceScopeFactory             _scopeFactory;

    public NotificationSender(IHttpClientFactory httpFactory, ILogger<NotificationSender> logger, IServiceScopeFactory scopeFactory)
    {
        _httpFactory  = httpFactory;
        _logger       = logger;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Dispatches the alert to the given channel.
    /// Returns <c>true</c> if the send succeeded (or was intentionally skipped for
    /// unsupported types); <c>false</c> if a transient error occurred so the caller
    /// can retry on the next cycle.
    /// </summary>
    public async Task<bool> SendAsync(NotificationChannel channel, AlertEvent alert, CancellationToken ct)
    {
        try
        {
            switch (channel.Type?.ToLowerInvariant())
            {
                case "email":
                    await SendEmailAsync(channel, alert, ct);
                    break;
                case "webhook":
                case "slack":
                case "teams":
                case "pagerduty":
                    await SendWebhookAsync(channel, alert, ct);
                    break;
                case "sms":
                    _logger.LogWarning("SMS channel id={Id} -- SMS gateway not configured, skipping", channel.Id);
                    break;
                default:
                    _logger.LogWarning("Unknown channel type '{Type}' for channel id={Id}", channel.Type, channel.Id);
                    break;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification via channel id={Id} type={Type}",
                channel.Id, channel.Type);
            return false;
        }
    }

    private async Task SendEmailAsync(NotificationChannel channel, AlertEvent alert, CancellationToken ct)
    {
        // Load global SMTP config from platformconfig table (category='notifications')
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();
        var rows = await db.PlatformConfigs
            .Where(c => c.Category == "notifications")
            .ToListAsync(ct);
        var cfg = rows.ToDictionary(c => c.Key ?? "", c => c.Value ?? "", StringComparer.OrdinalIgnoreCase);

        var smtpHost = cfg.GetValueOrDefault("SmtpHost", "localhost");
        var smtpPort = int.TryParse(cfg.GetValueOrDefault("SmtpPort", "587"), out var p) ? p : 587;
        var smtpTls  = cfg.GetValueOrDefault("SmtpTls", "true") != "false";
        var smtpUser = cfg.GetValueOrDefault("SmtpUser");
        var smtpPass = cfg.GetValueOrDefault("SmtpPass");
        var from     = cfg.GetValueOrDefault("FromAddress", "sentinellog@localhost");

        using var smtp = new SmtpClient(smtpHost, smtpPort) { EnableSsl = smtpTls };
        if (!string.IsNullOrEmpty(smtpUser) && !string.IsNullOrEmpty(smtpPass))
            smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);

        using var msg = new MailMessage(from, channel.Target ?? "")
        {
            Subject    = $"[SentinelLog] {alert.Severity} Alert: {alert.Message?.Truncate(80)}",
            Body       = $"Alert ID: {alert.Id}\nSeverity: {alert.Severity}\nTriggered: {alert.TriggeredAt}\n\n{alert.Message}",
            IsBodyHtml = false
        };

        await smtp.SendMailAsync(msg, ct);
        _logger.LogInformation("Email sent to {To} for alert id={Id}", channel.Target, alert.Id);
    }

    private async Task SendWebhookAsync(NotificationChannel channel, AlertEvent alert, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient();
        var payload = JsonSerializer.Serialize(new
        {
            text     = $"[{alert.Severity}] {alert.Message}",
            alertId  = alert.Id,
            severity = alert.Severity,
            message  = alert.Message,
            triggeredAt = alert.TriggeredAt?.ToString("o")
        });

        var content = new StringContent(payload, Encoding.UTF8, "application/json");
        var resp = await client.PostAsync(channel.Target, content, ct);
        resp.EnsureSuccessStatusCode();

        _logger.LogInformation("Webhook sent to {Url} for alert id={Id} -> {Status}",
            channel.Target, alert.Id, resp.StatusCode);
    }

    private static Dictionary<string, string> ParseConfig(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }
}

