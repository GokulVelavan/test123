namespace DRS.Scaffold.Syslog.Worker.Options;

/// <summary>Bound from the <c>WorkerOptions</c> configuration section.</summary>
public sealed class WorkerOptions
{
    public const string Section = "WorkerOptions";

    /// <summary>How often the poller wakes to check for new systemlogs rows (seconds).</summary>
    public int PollIntervalSeconds { get; set; } = 10;

    /// <summary>How often accumulated metrics are flushed to systemmetric (seconds).</summary>
    public int MetricFlushIntervalSeconds { get; set; } = 60;

    /// <summary>Maximum rows fetched from systemlogs per poll cycle.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>Value written to <c>createdby</c> on every inserted row.</summary>
    public string ServiceAccount { get; set; } = "syslog-worker";

    /// <summary>
    /// When <c>true</c> the watermark is initialised to <c>MAX(systemlogs.id)</c> at startup,
    /// meaning only rows arriving AFTER the service starts are processed.
    /// When <c>false</c> (default) the watermark starts at 0 and ALL existing rows are ingested.
    /// </summary>
    public bool StartFromCurrentWatermark { get; set; } = false;

    // ── Source Health ──────────────────────────────────────────────
    /// <summary>How often source health is evaluated (seconds).</summary>
    public int HealthCheckIntervalSeconds { get; set; } = 60;
    /// <summary>Minutes without logs before a source is marked Degraded.</summary>
    public int DegradedAfterMinutes { get; set; } = 5;
    /// <summary>Minutes without logs before a source is marked Offline.</summary>
    public int OfflineAfterMinutes { get; set; } = 15;

    // ── Notifications ──────────────────────────────────────────────
    /// <summary>How often unnotified alerts are dispatched (seconds).</summary>
    public int NotificationIntervalSeconds { get; set; } = 15;

    // ── Retention ──────────────────────────────────────────────────
    /// <summary>How often retention cleanup runs (seconds).</summary>
    public int RetentionIntervalSeconds { get; set; } = 3600;
    /// <summary>Default retention days when no per-source-type policy exists.</summary>
    public int DefaultRetentionDays { get; set; } = 90;

    // ── Scheduled Reports ──────────────────────────────────────────
    /// <summary>How often the report scheduler checks for due reports (seconds).</summary>
    public int ReportCheckIntervalSeconds { get; set; } = 60;

    // ── Export Jobs ────────────────────────────────────────────────
    /// <summary>How often pending export jobs are picked up (seconds).</summary>
    public int ExportCheckIntervalSeconds { get; set; } = 10;
    /// <summary>Directory where export files are written.</summary>
    public string ExportOutputDir { get; set; } = "exports";

    // ── Anomaly Detection ──────────────────────────────────────────
    /// <summary>How often anomaly rules are evaluated (seconds).</summary>
    public int AnomalyCheckIntervalSeconds { get; set; } = 300;

    // ── Log Forwarding ─────────────────────────────────────────────
    /// <summary>How often enabled log forwarders flush pending events (seconds).</summary>
    public int ForwarderIntervalSeconds { get; set; } = 30;
    /// <summary>Maximum events forwarded per forwarder per cycle.</summary>
    public int ForwarderBatchSize { get; set; } = 200;
}
