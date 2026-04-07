using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Worker.Metrics;
using DRS.Scaffold.Syslog.Worker.Options;
using DRS.Scaffold.Syslog.Worker.Services;
using DRS.Scaffold.Syslog.Worker.State;
using DRS.Scaffold.Syslog.Worker.Workers;
using Microsoft.EntityFrameworkCore;

var builder = Host.CreateApplicationBuilder(args);

// -- Service lifetime (auto-detect OS) ----------------------------------------
if (OperatingSystem.IsWindows())
    builder.Services.AddWindowsService(o => o.ServiceName = "DRS SentinelLog Syslog Worker");
else
    builder.Services.AddSystemd();

// -- Configuration ------------------------------------------------------------
builder.Services.Configure<WorkerOptions>(
    builder.Configuration.GetSection(WorkerOptions.Section));

// -- Database (scoped, consumed via IServiceScopeFactory in workers) ----------
builder.Services.AddDbContext<SyslogDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("SyslogDb")));

// -- HttpClient for notification webhooks -------------------------------------
builder.Services.AddHttpClient();

// -- Singleton state shared across poll cycles --------------------------------
builder.Services.AddSingleton<PollerWatermark>();
builder.Services.AddSingleton<MetricAccumulator>();

// -- Services -----------------------------------------------------------------
builder.Services.AddSingleton<NotificationSender>();

// -- Hosted workers -----------------------------------------------------------
builder.Services.AddHostedService<SyslogPollerWorker>();        // log ingestion + alert eval + IOC match
builder.Services.AddHostedService<SourceHealthWorker>();        // source status monitoring
builder.Services.AddHostedService<AlertNotificationWorker>();   // alert → channel dispatch
builder.Services.AddHostedService<LogRetentionWorker>();        // retention policy enforcement
builder.Services.AddHostedService<ScheduledReportWorker>();     // cron-based report generation
builder.Services.AddHostedService<ExportJobWorker>();           // async export processing
builder.Services.AddHostedService<AnomalyDetectionWorker>();    // anomaly rule evaluation
builder.Services.AddHostedService<LogForwarderWorker>();        // relay logs to external SIEMs

var host = builder.Build();

// ── Apply schema patches before workers start ─────────────────────────────────
// The worker runs independently from the API and must ensure required columns
// exist before any background workers query them (e.g. notificationchannel.name,
// alertevent.notifiedat). These are idempotent ADD COLUMN IF NOT EXISTS statements.
using (var startupScope = host.Services.CreateScope())
{
    var db     = startupScope.ServiceProvider.GetRequiredService<SyslogDbContext>();
    var logger = startupScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    var workerPatches = new[]
    {
        // Required by AlertNotificationWorker — must exist before first query
        "ALTER TABLE alertevent          ADD COLUMN IF NOT EXISTS notifiedat TIMESTAMPTZ NULL",
        "ALTER TABLE notificationchannel ADD COLUMN IF NOT EXISTS name       VARCHAR(100) NULL",
        // Required by AnomalyDetectionWorker / SyslogPollerWorker
        "ALTER TABLE anomalyrule  ADD COLUMN IF NOT EXISTS lastfiredat TIMESTAMPTZ NULL",
        "ALTER TABLE anomalyrule  ADD COLUMN IF NOT EXISTS firecount   BIGINT NOT NULL DEFAULT 0",
        // Required by SyslogPollerWorker (platform watermark persistence)
        @"CREATE TABLE IF NOT EXISTS platformconfig (
            id       BIGSERIAL    PRIMARY KEY,
            category VARCHAR(100) NULL,
            key      VARCHAR(100) NULL,
            value    TEXT         NULL,
            createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updatedat TIMESTAMPTZ NULL)",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_platformconfig_cat_key ON platformconfig (category, key)",
        // Incident / anomaly tables (needed if SyslogPollerWorker auto-creates incidents)
        @"CREATE TABLE IF NOT EXISTS anomalyrule (
            id BIGSERIAL PRIMARY KEY, name VARCHAR(200) NOT NULL, description TEXT NULL,
            metrictype VARCHAR(50) NOT NULL DEFAULT 'EpsTotal',
            baselinewindowminutes INTEGER NOT NULL DEFAULT 60,
            thresholdmultiplier DOUBLE PRECISION NOT NULL DEFAULT 3.0,
            absoluteminimum DOUBLE PRECISION NOT NULL DEFAULT 5.0,
            severity VARCHAR(20) NOT NULL DEFAULT 'High',
            autocreateincident BOOLEAN NOT NULL DEFAULT FALSE,
            enabled BOOLEAN NOT NULL DEFAULT TRUE, lastfiredat TIMESTAMPTZ NULL,
            firecount BIGINT NOT NULL DEFAULT 0, createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updatedat TIMESTAMPTZ NULL, isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        @"CREATE TABLE IF NOT EXISTS anomalyevent (
            id BIGSERIAL PRIMARY KEY, ruleid BIGINT NOT NULL REFERENCES anomalyrule(id) ON DELETE CASCADE,
            affectedentity VARCHAR(255) NULL, baselinevalue DOUBLE PRECISION NOT NULL DEFAULT 0,
            observedvalue DOUBLE PRECISION NOT NULL DEFAULT 0, deviationratio DOUBLE PRECISION NOT NULL DEFAULT 0,
            details TEXT NULL, acknowledged BOOLEAN NOT NULL DEFAULT FALSE,
            acknowledgedby VARCHAR(100) NULL, acknowledgedat TIMESTAMPTZ NULL,
            detectedat TIMESTAMPTZ NOT NULL DEFAULT NOW(), isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        @"CREATE TABLE IF NOT EXISTS incident (
            id BIGSERIAL PRIMARY KEY, title VARCHAR(200) NOT NULL, description TEXT NULL,
            status VARCHAR(30) NOT NULL DEFAULT 'Open', priority VARCHAR(20) NOT NULL DEFAULT 'Medium',
            assigneeid BIGINT NULL, alerteventid BIGINT NULL, tags TEXT NULL,
            notes TEXT NULL, affectedhosts TEXT NULL, resolvedat TIMESTAMPTZ NULL,
            resolvedbyid BIGINT NULL, createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updatedat TIMESTAMPTZ NULL, createdbyid BIGINT NULL,
            isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        // Required by LogForwarderWorker
        @"CREATE TABLE IF NOT EXISTS logforwarder (
            id BIGSERIAL PRIMARY KEY, name VARCHAR(150) NOT NULL,
            protocol VARCHAR(10) NOT NULL DEFAULT 'UDP', host VARCHAR(255) NOT NULL DEFAULT '',
            port INTEGER NOT NULL DEFAULT 514, format VARCHAR(20) NOT NULL DEFAULT 'RFC5424',
            severityfilter VARCHAR(100) NULL, sourcefilter TEXT NULL,
            enabled BOOLEAN NOT NULL DEFAULT TRUE, tlscertpath TEXT NULL, description TEXT NULL,
            forwardedcount BIGINT NOT NULL DEFAULT 0, errorcount BIGINT NOT NULL DEFAULT 0,
            lastforwardedat TIMESTAMPTZ NULL, createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updatedat TIMESTAMPTZ NULL, isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        // Threat Intel (referenced by SyslogPollerWorker IOC checks)
        @"CREATE TABLE IF NOT EXISTS threatindicator (
            id BIGSERIAL PRIMARY KEY, type VARCHAR(20) NOT NULL DEFAULT 'IP',
            value VARCHAR(512) NOT NULL, threatcategory VARCHAR(50) NULL,
            confidence INTEGER NOT NULL DEFAULT 80, source VARCHAR(100) NULL,
            description TEXT NULL, origin VARCHAR(20) NULL DEFAULT 'Manual',
            isactive BOOLEAN NOT NULL DEFAULT TRUE, expiresat TIMESTAMPTZ NULL,
            createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(), updatedat TIMESTAMPTZ NULL,
            isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        // GeoIP and MFA columns on existing tables
        "ALTER TABLE source   ADD COLUMN IF NOT EXISTS geocountrycode VARCHAR(2)   NULL",
        "ALTER TABLE source   ADD COLUMN IF NOT EXISTS geocountry     VARCHAR(100) NULL",
        "ALTER TABLE source   ADD COLUMN IF NOT EXISTS geocity        VARCHAR(100) NULL",
        "ALTER TABLE source   ADD COLUMN IF NOT EXISTS geolat         DOUBLE PRECISION NULL",
        "ALTER TABLE source   ADD COLUMN IF NOT EXISTS geolon         DOUBLE PRECISION NULL",
        "ALTER TABLE source   ADD COLUMN IF NOT EXISTS geoasn         VARCHAR(60)  NULL",
        "ALTER TABLE appuser  ADD COLUMN IF NOT EXISTS mfasecret      VARCHAR(128) NULL",
        "ALTER TABLE appuser  ADD COLUMN IF NOT EXISTS mfaenabled     BOOLEAN NOT NULL DEFAULT FALSE",
        // Ensure missing columns on incident if previously created without them
        "ALTER TABLE incident ADD COLUMN IF NOT EXISTS resolvedbyid BIGINT NULL",
        "ALTER TABLE incident ADD COLUMN IF NOT EXISTS createdbyid  BIGINT NULL",
    };

    foreach (var sql in workerPatches)
    {
        try { await db.Database.ExecuteSqlRawAsync(sql); }
        catch (Exception ex) { logger.LogWarning("Worker patch skipped: {Msg}", ex.Message.Split('\n')[0]); }
    }

    logger.LogInformation("Worker schema patches complete.");
}

host.Run();
