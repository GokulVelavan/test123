using DRS.Scaffold.Syslog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.Core.Data;

public class SyslogDbContext : DbContext
{
    public SyslogDbContext(DbContextOptions<SyslogDbContext> options) : base(options) { }

    // ?? All 14 tables ?????????????????????????????????????????????????????????
    public DbSet<LogSource>           Sources              { get; set; }
    public DbSet<LogEvent>            LogEvents            { get; set; }
    public DbSet<AlertRule>           AlertRules           { get; set; }
    public DbSet<AlertEvent>          AlertEvents          { get; set; }
    public DbSet<AlertNotification>   AlertNotifications   { get; set; }
    public DbSet<NotificationChannel> NotificationChannels { get; set; }
    public DbSet<AppUser>             AppUsers             { get; set; }
    public DbSet<UserSession>         UserSessions         { get; set; }
    public DbSet<Role>                Roles                { get; set; }
    public DbSet<Permission>          Permissions          { get; set; }
    public DbSet<RolePermission>      RolePermissions      { get; set; }
    public DbSet<UserRole>            UserRoles            { get; set; }
    public DbSet<ExportJob>           ExportJobs           { get; set; }
    public DbSet<StoragePolicy>       StoragePolicies      { get; set; }
    public DbSet<SystemMetric>        SystemMetrics        { get; set; }
    public DbSet<AuditLog>            SystemLogs           { get; set; }
    // New tables added for skipped items
    public DbSet<ApiKey>              ApiKeys              { get; set; }
    public DbSet<License>             Licenses             { get; set; }
    public DbSet<PlatformConfig>      PlatformConfigs      { get; set; }
    // #79 — scheduled reports
    public DbSet<ScheduledReport>     ScheduledReports     { get; set; }
    // #79 — source groups
    public DbSet<SourceGroup>         SourceGroups         { get; set; }
    public DbSet<SourceGroupMember>   SourceGroupMembers   { get; set; }
    // New modules: Incidents, SavedSearches, ThreatIntel, LogForwarders
    public DbSet<Incident>            Incidents            { get; set; }
    public DbSet<SavedSearch>         SavedSearches        { get; set; }
    public DbSet<ThreatIndicator>     ThreatIndicators     { get; set; }
    public DbSet<LogForwarder>        LogForwarders        { get; set; }
    // Anomaly Detection
    public DbSet<AnomalyRule>         AnomalyRules         { get; set; }
    public DbSet<AnomalyEvent>        AnomalyEvents        { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ?? source ????????????????????????????????????????????????????????????
        modelBuilder.Entity<LogSource>(e =>
        {
            e.ToTable("source");
            // Same inet ? text cast as usersession.ipaddress
            e.Property(s => s.IpAddress)
             .HasColumnType("text")
             .HasConversion(v => v, v => v);
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.DeviceType);
            e.HasQueryFilter(s => !s.IsDeleted);
        });

        // ?? logevent ??????????????????????????????????????????????????????????
        modelBuilder.Entity<LogEvent>(e =>
        {
            e.ToTable("logevent");
            e.HasIndex(l => l.EventTime);
            e.HasIndex(l => l.Severity);
            e.HasIndex(l => l.Hostname);
            e.HasIndex(l => l.SourceId);
            e.HasOne(l => l.Source)
             .WithMany(s => s.LogEvents)
             .HasForeignKey(l => l.SourceId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(l => !l.IsDeleted);
        });

        // ?? alertrule ?????????????????????????????????????????????????????????
        modelBuilder.Entity<AlertRule>(e =>
        {
            e.ToTable("alertrule");
            e.HasIndex(a => a.Enabled);
            e.HasIndex(a => a.Severity);
            e.HasQueryFilter(a => !a.IsDeleted);
        });

        // ?? alertevent ????????????????????????????????????????????????????????
        modelBuilder.Entity<AlertEvent>(e =>
        {
            e.ToTable("alertevent");
            e.HasIndex(a => a.TriggeredAt);
            e.HasIndex(a => a.Acknowledged);
            e.HasOne(a => a.Rule)
             .WithMany(r => r.AlertEvents)
             .HasForeignKey(a => a.RuleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(a => a.LogEvent)
             .WithMany(l => l.AlertEvents)
             .HasForeignKey(a => a.LogEventId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(a => !a.IsDeleted);
        });

        // ?? notificationchannel ???????????????????????????????????????????????
        modelBuilder.Entity<NotificationChannel>(e =>
        {
            e.ToTable("notificationchannel");
            // jsonb column
            e.Property(n => n.Config).HasColumnType("jsonb");
            e.HasIndex(n => n.Type);
            e.HasQueryFilter(n => !n.IsDeleted);
        });

        // ?? alert_notification ????????????????????????????????????????????????
        modelBuilder.Entity<AlertNotification>(e =>
        {
            e.ToTable("alert_notification");
            e.HasOne(an => an.Rule)
             .WithMany(r => r.AlertNotifications)
             .HasForeignKey(an => an.RuleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(an => an.NotificationChannel)
             .WithMany(nc => nc.AlertNotifications)
             .HasForeignKey(an => an.NotificationId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(an => !an.IsDeleted);
        });

        // ?? appuser ???????????????????????????????????????????????????????????
        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("appuser");
            e.HasIndex(u => u.Username).IsUnique();
            e.HasIndex(u => u.Email).IsUnique();
            e.HasQueryFilter(u => !u.IsDeleted);
        });

        // ?? usersession ???????????????????????????????????????????????????????
        modelBuilder.Entity<UserSession>(e =>
        {
            e.ToTable("usersession");
            // Cast the PostgreSQL inet column to text so EF can read/write plain
            // varchar strings without a type mismatch. The USING clause in the
            // migration below handles the one-time schema conversion.
            e.Property(s => s.IpAddress)
             .HasColumnType("text")
             .HasConversion(
                 v => v,          // string ? DB: pass through
                 v => v);         // DB ? string: pass through
            e.HasIndex(s => s.RefreshToken);
            e.HasIndex(s => s.ExpiresAt);
            e.HasOne(s => s.User)
             .WithMany(u => u.UserSessions)
             .HasForeignKey(s => s.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(s => !s.IsDeleted);
        });

        // ?? role ??????????????????????????????????????????????????????????????
        modelBuilder.Entity<Role>(e =>
        {
            e.ToTable("role");
            e.HasIndex(r => r.Name).IsUnique();
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ?? permission ????????????????????????????????????????????????????????
        modelBuilder.Entity<Permission>(e =>
        {
            e.ToTable("permission");
            e.HasIndex(p => p.Name).IsUnique();
            e.HasQueryFilter(p => !p.IsDeleted);
        });

        // ?? role_permission ???????????????????????????????????????????????????
        modelBuilder.Entity<RolePermission>(e =>
        {
            e.ToTable("role_permission");
            e.HasOne(rp => rp.Role)
             .WithMany(r => r.RolePermissions)
             .HasForeignKey(rp => rp.RoleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(rp => rp.Permission)
             .WithMany(p => p.RolePermissions)
             .HasForeignKey(rp => rp.PermissionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(rp => !rp.IsDeleted);
        });

        // ?? user_role ?????????????????????????????????????????????????????????
        modelBuilder.Entity<UserRole>(e =>
        {
            e.ToTable("user_role");
            e.HasOne(ur => ur.User)
             .WithMany(u => u.UserRoles)
             .HasForeignKey(ur => ur.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ur => ur.Role)
             .WithMany(r => r.UserRoles)
             .HasForeignKey(ur => ur.RoleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(ur => !ur.IsDeleted);
        });

        // ?? exportjob ?????????????????????????????????????????????????????????
        modelBuilder.Entity<ExportJob>(e =>
        {
            e.ToTable("exportjob");
            // jsonb column
            e.Property(j => j.QueryDefinition).HasColumnType("jsonb");
            e.HasIndex(j => j.Status);
            e.HasOne(j => j.User)
             .WithMany(u => u.ExportJobs)
             .HasForeignKey(j => j.UserId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(j => !j.IsDeleted);
        });

        // ?? storagepolicy ?????????????????????????????????????????????????????
        modelBuilder.Entity<StoragePolicy>(e =>
        {
            e.ToTable("storagepolicy");
            e.HasIndex(sp => sp.SourceType);
            e.HasQueryFilter(sp => !sp.IsDeleted);
        });

        // ?? systemmetric ??????????????????????????????????????????????????????
        modelBuilder.Entity<SystemMetric>(e =>
        {
            e.ToTable("systemmetric");
            e.HasIndex(m => m.MetricTime);
            e.HasIndex(m => m.Name);
            e.HasQueryFilter(m => !m.IsDeleted);
        });

        // ?? systemlogs � append-only, no soft-delete filter ???????????????????
        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("systemlogs");
            e.HasIndex(sl => sl.LogDatetime);
            e.HasIndex(sl => sl.Host);
        });

        // ?? apikey
        modelBuilder.Entity<ApiKey>(e =>
        {
            e.ToTable("apikey");
            e.HasIndex(k => k.Enabled);
            e.HasQueryFilter(k => !k.IsDeleted);
        });

        // ?? license
        modelBuilder.Entity<License>(e =>
        {
            e.ToTable("license");
            e.HasQueryFilter(l => !l.IsDeleted);
        });

        // ?? platformconfig
        modelBuilder.Entity<PlatformConfig>(e =>
        {
            e.ToTable("platformconfig");
            e.HasIndex(c => new { c.Category, c.Key }).IsUnique();
        });

        // ?? scheduledreport
        modelBuilder.Entity<ScheduledReport>(e =>
        {
            e.ToTable("scheduledreport");
            e.HasIndex(r => r.Enabled);
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ?? sourcegroup
        modelBuilder.Entity<SourceGroup>(e =>
        {
            e.ToTable("sourcegroup");
            e.HasIndex(g => g.Name);
            e.HasQueryFilter(g => !g.IsDeleted);
        });

        // ?? sourcegroupmember
        modelBuilder.Entity<SourceGroupMember>(e =>
        {
            e.ToTable("sourcegroupmember");
            e.HasIndex(m => new { m.GroupId, m.SourceId }).IsUnique();
            e.HasOne(m => m.Group)
             .WithMany(g => g.Members)
             .HasForeignKey(m => m.GroupId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.Source)
             .WithMany()
             .HasForeignKey(m => m.SourceId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ?? incident
        modelBuilder.Entity<Incident>(e =>
        {
            e.ToTable("incident");
            e.HasIndex(i => i.Status);
            e.HasIndex(i => i.Priority);
            e.HasIndex(i => i.CreatedAt);
            e.HasQueryFilter(i => !i.IsDeleted);
        });

        // ?? savedsearch
        modelBuilder.Entity<SavedSearch>(e =>
        {
            e.ToTable("savedsearch");
            e.Property(s => s.QueryJson).HasColumnType("jsonb");
            e.HasIndex(s => s.UserId);
            e.HasIndex(s => s.IsShared);
            e.HasQueryFilter(s => !s.IsDeleted);
        });

        // ?? threatindicator
        modelBuilder.Entity<ThreatIndicator>(e =>
        {
            e.ToTable("threatindicator");
            e.HasIndex(t => t.Type);
            e.HasIndex(t => t.Value);
            e.HasIndex(t => t.IsActive);
            e.HasQueryFilter(t => !t.IsDeleted);
        });

        // ?? logforwarder
        modelBuilder.Entity<LogForwarder>(e =>
        {
            e.ToTable("logforwarder");
            e.HasIndex(f => f.Enabled);
            e.HasQueryFilter(f => !f.IsDeleted);
        });

        // ?? anomalyrule
        modelBuilder.Entity<AnomalyRule>(e =>
        {
            e.ToTable("anomalyrule");
            e.HasIndex(r => r.Enabled);
            e.HasIndex(r => r.MetricType);
            e.HasQueryFilter(r => !r.IsDeleted);
        });

        // ?? anomalyevent
        modelBuilder.Entity<AnomalyEvent>(e =>
        {
            e.ToTable("anomalyevent");
            e.HasIndex(ev => ev.DetectedAt);
            e.HasIndex(ev => ev.Acknowledged);
            e.HasOne(ev => ev.Rule)
             .WithMany()
             .HasForeignKey(ev => ev.RuleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(ev => !ev.IsDeleted);
        });
    }
}
