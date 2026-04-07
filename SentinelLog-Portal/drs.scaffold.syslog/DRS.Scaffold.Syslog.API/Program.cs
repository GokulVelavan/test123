using DRS.Scaffold.Syslog.API;
using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Interfaces.Managers;
using DRS.Scaffold.Syslog.Core.Interfaces.Repositories;
using DRS.Scaffold.Syslog.Core.Options;
using DRS.Scaffold.Syslog.Middleware.Managers;
using DRS.Scaffold.Syslog.Middleware.Repositories;
using DRS.Scaffold.Syslog.Middleware.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ?? JWT settings ??????????????????????????????????????????????????????????????
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection(JwtSettings.Section));

var jwtCfg = builder.Configuration
    .GetSection(JwtSettings.Section)
    .Get<JwtSettings>()!;

// ?? JWT authentication middleware ?????????????????????????????????????????????
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey  = true,
            ClockSkew                = TimeSpan.FromSeconds(30),
            ValidIssuer              = jwtCfg.Issuer,
            ValidAudience            = jwtCfg.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtCfg.Key))
        };
    });

builder.Services.AddAuthorization();

// ?? Database ??????????????????????????????????????????????????????????????????
builder.Services.AddDbContext<SyslogDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("SyslogDb"),
        npgsql => npgsql.MigrationsAssembly("DRS.Scaffold.Syslog.API")));

// ?? Repositories ??????????????????????????????????????????????????????????????
builder.Services.AddScoped<ILogSourceRepository,  LogSourceRepository>();
builder.Services.AddScoped<ILogEntryRepository,   LogEntryRepository>();
builder.Services.AddScoped<IAlertRepository,      AlertRepository>();
builder.Services.AddScoped<IUserRepository,       UserRepository>();
builder.Services.AddScoped<ISystemLogRepository,  SystemLogRepository>();
builder.Services.AddScoped<ISessionRepository,    SessionRepository>();

// ?? Services ??????????????????????????????????????????????????????????????????
builder.Services.AddScoped<JwtService>();

// ?? Managers ??????????????????????????????????????????????????????????????????
builder.Services.AddScoped<ILogSourceManager,          LogSourceManager>();
builder.Services.AddScoped<ILogEntryManager,           LogEntryManager>();
builder.Services.AddScoped<IAlertManager,              AlertManager>();
builder.Services.AddScoped<IUserManager,               UserManager>();
builder.Services.AddScoped<ISystemLogManager,          SystemLogManager>();
builder.Services.AddScoped<ISettingsManager,           SettingsManager>();
// New managers for skipped items
builder.Services.AddScoped<NotificationChannelRepository>();
builder.Services.AddScoped<INotificationChannelManager, NotificationChannelManager>();
builder.Services.AddScoped<IApiKeyManager,              ApiKeyManager>();
builder.Services.AddScoped<ILicenseManager,             LicenseManager>();
builder.Services.AddScoped<IScheduledReportRepository,  ScheduledReportRepository>();
builder.Services.AddScoped<IReportsManager,             ReportsManager>();
builder.Services.AddScoped<ISourceGroupRepository,      SourceGroupRepository>();
builder.Services.AddScoped<ISourceGroupManager,         SourceGroupManager>();
// Anomaly Detection
builder.Services.AddScoped<IAnomalyManager,             AnomalyManager>();
// New modules: Incidents, SavedSearches, ThreatIntel, LogForwarders
builder.Services.AddScoped<IIncidentRepository,         IncidentRepository>();
builder.Services.AddScoped<IIncidentManager,            IncidentManager>();
builder.Services.AddScoped<ISavedSearchRepository,      SavedSearchRepository>();
builder.Services.AddScoped<ISavedSearchManager,         SavedSearchManager>();
builder.Services.AddScoped<IThreatIntelRepository,      ThreatIntelRepository>();
builder.Services.AddScoped<IThreatIntelManager,         ThreatIntelManager>();
builder.Services.AddScoped<ILogForwarderRepository,     LogForwarderRepository>();
builder.Services.AddScoped<ILogForwarderManager,        LogForwarderManager>();

// ?? Controllers + JSON ????????????????????????????????????????????????????????
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// ?? Swagger / OpenAPI ?????????????????????????????????????????????????????????
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "SentinelLog Syslog API",
        Version     = "v1",
        Description = """
            REST API for the **SentinelLog Enterprise Syslog Platform**.

            ### Functional areas
            | Tag | Description |
            |-----|-------------|
            | Logs | Live feed, search, dashboard KPIs, analytics charts |
            | Sources | Log source (device) inventory CRUD |
            | Alerts | Alert rule configuration and fired-event management |
            | Users | User account management (appuser table) |
            | Auth | Login, token refresh, logout and MFA verification |
            | Settings | Platform settings, storage policies, system metrics and logs |

            ### Notes
            - All timestamps are **ISO-8601 with UTC offset** (`DateTimeOffset`).
            - Syslog `severity` and `facility` are `short` values per **RFC 5424** (0�7 / 0�23).
            - Soft-deleted records are excluded from all queries via a global EF query filter.
            """,
        Contact = new OpenApiContact { Name = "SentinelLog Platform Team", Email = "noc@acme.corp" }
    });

    c.TagActionsBy(api => [api.GroupName ?? api.ActionDescriptor.RouteValues["controller"]!]);
    c.DocInclusionPredicate((_, _) => true);

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter your JWT token. Example: **eyJhbGci...**"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    c.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
});

// ?? CORS ??????????????????????????????????????????????????????????????????????
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddPolicy("SyslogCors", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()));

// ?????????????????????????????????????????????????????????????????????????????
var app = builder.Build();

// ?? Auto-migrate + seed ???????????????????????????????????????????????????????
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

    try { db.Database.Migrate(); }
    catch (Exception ex) { app.Logger.LogWarning(ex, "EF Migrate() had errors — applying schema patches manually."); }

    // ── Idempotent schema patches — each statement runs independently ─────────
    var patches = new[]
    {
        "ALTER TABLE appuser ADD COLUMN IF NOT EXISTS mfaenabled BOOLEAN NOT NULL DEFAULT false",
        "ALTER TABLE appuser ADD COLUMN IF NOT EXISTS mfasecret VARCHAR(128) NULL",
        "ALTER TABLE source ADD COLUMN IF NOT EXISTS geocountrycode VARCHAR(2) NULL",
        "ALTER TABLE source ADD COLUMN IF NOT EXISTS geocountry VARCHAR(100) NULL",
        "ALTER TABLE source ADD COLUMN IF NOT EXISTS geocity VARCHAR(100) NULL",
        "ALTER TABLE source ADD COLUMN IF NOT EXISTS geolat DOUBLE PRECISION NULL",
        "ALTER TABLE source ADD COLUMN IF NOT EXISTS geolon DOUBLE PRECISION NULL",
        "ALTER TABLE source ADD COLUMN IF NOT EXISTS geoasn VARCHAR(60) NULL",
        "ALTER TABLE alertevent ADD COLUMN IF NOT EXISTS notifiedat TIMESTAMPTZ NULL",
        "ALTER TABLE notificationchannel ADD COLUMN IF NOT EXISTS name VARCHAR(100) NULL",
        @"CREATE TABLE IF NOT EXISTS apikey (
            id BIGSERIAL PRIMARY KEY, name VARCHAR(100) NOT NULL, keyprefix VARCHAR(10) NOT NULL,
            keyhash VARCHAR(256) NOT NULL, enabled BOOLEAN NOT NULL DEFAULT TRUE,
            createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(), isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        @"CREATE TABLE IF NOT EXISTS license (
            id BIGSERIAL PRIMARY KEY, licensekey TEXT NULL, tier VARCHAR(50) NOT NULL DEFAULT 'Community',
            maxsources INTEGER NOT NULL DEFAULT 5, maxusers INTEGER NOT NULL DEFAULT 3,
            expiresat TIMESTAMPTZ NULL, createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(), isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        @"CREATE TABLE IF NOT EXISTS platformconfig (
            id BIGSERIAL PRIMARY KEY, category VARCHAR(100) NULL, key VARCHAR(100) NULL,
            value TEXT NULL, createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(), updatedat TIMESTAMPTZ NULL)",
        "CREATE UNIQUE INDEX IF NOT EXISTS ix_platformconfig_cat_key ON platformconfig (category, key)",
        @"CREATE TABLE IF NOT EXISTS scheduledreport (
            id BIGSERIAL PRIMARY KEY, name VARCHAR(200) NOT NULL, schedule VARCHAR(100) NOT NULL,
            filtersjson JSONB NULL, format VARCHAR(20) NOT NULL DEFAULT 'CSV',
            enabled BOOLEAN NOT NULL DEFAULT TRUE, lastrunathz TIMESTAMPTZ NULL,
            createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(), isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        @"CREATE TABLE IF NOT EXISTS sourcegroup (
            id BIGSERIAL PRIMARY KEY, name VARCHAR(150) NOT NULL,
            description TEXT NULL, createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(), isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        @"CREATE TABLE IF NOT EXISTS sourcegroupmember (
            id BIGSERIAL PRIMARY KEY, groupid BIGINT NOT NULL REFERENCES sourcegroup(id) ON DELETE CASCADE,
            sourceid BIGINT NOT NULL REFERENCES source(id) ON DELETE CASCADE)",
        @"CREATE TABLE IF NOT EXISTS incident (
            id BIGSERIAL PRIMARY KEY, title VARCHAR(200) NOT NULL, description TEXT NULL,
            status VARCHAR(30) NOT NULL DEFAULT 'Open', priority VARCHAR(20) NOT NULL DEFAULT 'Medium',
            assigneeid BIGINT NULL, alerteventid BIGINT NULL, tags TEXT NULL,
            notes TEXT NULL, affectedhosts TEXT NULL, resolvedat TIMESTAMPTZ NULL,
            resolvedbyid BIGINT NULL, createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updatedat TIMESTAMPTZ NULL, createdbyid BIGINT NULL,
            isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        // Ensure resolvedbyid / createdbyid exist when the table was previously created without them
        "ALTER TABLE incident ADD COLUMN IF NOT EXISTS resolvedbyid BIGINT NULL",
        "ALTER TABLE incident ADD COLUMN IF NOT EXISTS createdbyid  BIGINT NULL",
        @"CREATE TABLE IF NOT EXISTS savedsearch (
            id BIGSERIAL PRIMARY KEY, name VARCHAR(200) NOT NULL, description TEXT NULL,
            queryjson JSONB NOT NULL DEFAULT '{}', userid BIGINT NULL, isshared BOOLEAN NOT NULL DEFAULT FALSE,
            createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(), isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        @"CREATE TABLE IF NOT EXISTS threatindicator (
            id BIGSERIAL PRIMARY KEY, type VARCHAR(20) NOT NULL, value VARCHAR(500) NOT NULL,
            threatcategory VARCHAR(100) NULL, confidence INTEGER NOT NULL DEFAULT 80,
            source VARCHAR(200) NULL, description TEXT NULL, origin VARCHAR(50) NULL DEFAULT 'Manual',
            isactive BOOLEAN NOT NULL DEFAULT TRUE, expiresat TIMESTAMPTZ NULL,
            createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(), updatedat TIMESTAMPTZ NULL,
            isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
        // Ensure updatedat exists when the table was previously created without it
        "ALTER TABLE threatindicator ADD COLUMN IF NOT EXISTS updatedat TIMESTAMPTZ NULL",
        "ALTER TABLE savedsearch     ADD COLUMN IF NOT EXISTS updatedat TIMESTAMPTZ NULL",
        @"CREATE TABLE IF NOT EXISTS logforwarder (
            id BIGSERIAL PRIMARY KEY, name VARCHAR(150) NOT NULL,
            protocol VARCHAR(10) NOT NULL DEFAULT 'UDP', host VARCHAR(255) NOT NULL DEFAULT '',
            port INTEGER NOT NULL DEFAULT 514, format VARCHAR(20) NOT NULL DEFAULT 'RFC5424',
            severityfilter VARCHAR(100) NULL, sourcefilter TEXT NULL,
            enabled BOOLEAN NOT NULL DEFAULT TRUE, tlscertpath TEXT NULL, description TEXT NULL,
            forwardedcount BIGINT NOT NULL DEFAULT 0, errorcount BIGINT NOT NULL DEFAULT 0,
            lastforwardedat TIMESTAMPTZ NULL, createdat TIMESTAMPTZ NOT NULL DEFAULT NOW(),
            updatedat TIMESTAMPTZ NULL, isdeleted BOOLEAN NOT NULL DEFAULT FALSE)",
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
    };

    foreach (var sql in patches)
    {
        try { await db.Database.ExecuteSqlRawAsync(sql); }
        catch (Exception ex) { app.Logger.LogWarning("Patch skipped: {Msg}", ex.Message.Split('\n')[0]); }
    }
    app.Logger.LogInformation("Schema patches complete.");

    // ── Seed admin user if table is empty ──────────────────────────────────────
    bool forceSeed = args.Contains("--seed-users");
    bool isEmpty   = !db.AppUsers.Any();

    if (forceSeed || isEmpty)
        await SeedUser.RunAsync(app.Services, app.Logger);
}

// ?? Swagger UI ????????????????????????????????????????????????????????????????
app.UseSwagger(o => { o.RouteTemplate = "swagger/{documentName}/swagger.json"; });
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SentinelLog API v1");
    c.RoutePrefix   = "swagger";
    c.DocumentTitle = "SentinelLog API";
    c.DefaultModelsExpandDepth(1);
    c.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Model);
    c.DisplayRequestDuration();
    c.EnableFilter();
    c.EnableDeepLinking();
});

// ?? Middleware pipeline ???????????????????????????????????????????????????????
app.UseHttpsRedirection();
app.UseCors("SyslogCors");
app.UseAuthentication();   // ? must come before UseAuthorization
app.UseAuthorization();
app.MapControllers();
app.Run();
