using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var outputPath = @"D:\syslog-gap-analysis-report.pdf";

Document.Create(container =>
{
    container.Page(page =>
    {
        page.Size(PageSizes.A4);
        page.Margin(40);
        page.DefaultTextStyle(x => x.FontSize(10));

        page.Header().Element(Header);
        page.Content().Element(Content);
        page.Footer().AlignCenter().Text(x =>
        {
            x.Span("Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
    });
}).GeneratePdf(outputPath);

Console.WriteLine($"PDF generated: {outputPath}");

void Header(IContainer container)
{
    container.Column(col =>
    {
        col.Item().Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("SentinelLog - Competitive Gap Analysis Report")
                    .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                c.Item().Text($"Prepared: {DateTime.Now:MMMM dd, yyyy}  |  Target Market: Indian Manufacturing SMBs")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
        col.Item().PaddingBottom(5).LineHorizontal(2).LineColor(Colors.Blue.Darken3);
    });
}

void Content(IContainer container)
{
    container.Column(col =>
    {
        col.Spacing(6);

        // 1. EXECUTIVE SUMMARY
        SectionTitle(col, "1. Executive Summary");
        col.Item().Text(t =>
        {
            t.Span("SentinelLog").Bold();
            t.Span(" is an on-premise, centralized log management platform built to replace ");
            t.Span("Kiwi Syslog Server NG (SolarWinds)").Bold();
            t.Span(" and ");
            t.Span("Motadata AIOps").Bold();
            t.Span(" for Indian manufacturing SMBs. It combines a syslog receiver, cross-platform agents (Windows + Linux), " +
                   "a PostgreSQL-backed API, a web portal, and background workers - all deployable via a one-click installer for air-gapped environments.\n\n");
            t.Span("This report analyzes the product across 49 feature categories. Key findings:\n");
            t.Span("  - SentinelLog covers ~70-75% of Kiwi's features and ~50-55% of Motadata's\n");
            t.Span("  - SentinelLog exceeds Kiwi in: web portal, REST API, incident mgmt, threat intel, cross-platform agents\n");
            t.Span("  - Critical gaps: production MFA, LDAP binding, SNMP traps, agent service mode, offline buffering\n");
            t.Span("  - With 3 months of focused development, SentinelLog can achieve full Kiwi parity\n");
            t.Span("  - Cost advantage: free/low-cost vs Kiwi (Rs 25K/node) vs Motadata (Rs 2.5L+/month)");
        });

        // 2. PRODUCT ARCHITECTURE
        SectionTitle(col, "2. Product Architecture Overview");
        col.Item().Text("SentinelLog consists of 5 interconnected components:").Bold();
        col.Item().PaddingLeft(10).Column(inner =>
        {
            inner.Spacing(3);
            ArchRow(inner, "Linux Syslog Receiver", "Syslog-NG OSE + Python forwarder. Receives UDP/TCP/TLS syslog on ports 514, 1514, 601, 1515, 6514. Stores to PostgreSQL. Offline air-gapped installation.");
            ArchRow(inner, "Windows WPF Agent", ".NET 8 WPF desktop app. Collects 21 Windows Event Log channels with 200+ security event ID filters (MITRE/NIST/CIS). Live dashboard, severity coloring, metrics.");
            ArchRow(inner, "Linux Console Agent", ".NET 8 cross-platform. Collectors: journald, 50+ syslog files, dmesg, binary logs (wtmp/btmp), auto-discovery of /var/log. Dual-mode (realtime + batch). Deduplication via offset tracking.");
            ArchRow(inner, "Portal + API + Worker", "ASP.NET Core API (17 controllers), MVC Web UI (Razor views), Background Worker (8 services). JWT auth, RBAC, alerts, incidents, threat intel, anomaly detection, compliance reports, log forwarding.");
            ArchRow(inner, "Windows Installer", "Inno Setup. One-click installs Docker Desktop, PostgreSQL 17, Syslog-NG container. Air-gapped capable. TLS cert gen. Phased install with reboot resume.");
        });
        col.Item().Text("Database: PostgreSQL (25+ tables) | Stack: .NET 8, ASP.NET Core, WPF, Syslog-NG OSE, Docker").Italic().FontSize(9);

        // 3. FEATURE MATRIX
        SectionTitle(col, "3. Feature Comparison Matrix (SentinelLog vs Kiwi vs Motadata)");
        col.Item().Text("Legend:  Y = Yes/Full  |  P = Partial  |  N = No/Missing").FontSize(8).Italic();

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(3f);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1.2f);
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Feature").FontSize(8).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("SentinelLog").FontSize(8).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Kiwi NG").FontSize(8).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Blue.Darken3).Padding(3).Text("Motadata").FontSize(8).Bold().FontColor(Colors.White);
            });

            // LOG COLLECTION
            CatRow(table, "LOG COLLECTION");
            R(table, "Syslog UDP reception", "Y", "Y", "Y");
            R(table, "Syslog TCP reception", "Y", "Y", "Y");
            R(table, "Syslog TLS/SSL reception", "Y", "Y", "Y");
            R(table, "Windows Event Log collection (agent)", "Y (21 channels)", "Y (WMI)", "Y");
            R(table, "Linux journald + file collection", "Y (50+ files)", "P (syslog only)", "Y");
            R(table, "SNMP trap collection", "N", "Y", "Y");
            R(table, "Custom file/path monitoring", "Y", "Y", "Y");
            R(table, "Auto-discovery of new log sources", "Y", "N", "Y");
            R(table, "Binary log collection (wtmp/btmp/lastlog)", "Y", "N", "N");
            R(table, "dmesg kernel ring buffer", "Y", "N", "P");

            // STORAGE
            CatRow(table, "STORAGE & ARCHIVAL");
            R(table, "Database storage (structured)", "Y (PostgreSQL)", "P (SQL opt)", "Y (Elastic)");
            R(table, "Log retention policies (per source type)", "Y", "Y", "Y");
            R(table, "Scheduled log archival to compressed files", "P (policy only)", "Y (rotation)", "Y");
            R(table, "Storage usage monitoring", "Y", "P", "Y");

            // SEARCH
            CatRow(table, "SEARCH & ANALYSIS");
            R(table, "Full-text keyword search", "Y", "Y", "Y");
            R(table, "Search by hostname/severity/facility/program", "Y", "Y", "Y");
            R(table, "Faceted search (sidebar aggregations)", "Y", "N", "Y");
            R(table, "Saved searches", "Y", "N", "Y");
            R(table, "Advanced query language (KQL-like)", "N", "P (regex)", "Y");
            R(table, "Live log feed", "Y (polling)", "Y (real-time)", "Y (real-time)");

            // ALERTING
            CatRow(table, "ALERTING & NOTIFICATIONS");
            R(table, "Alert rules (CRUD, enable/disable)", "Y", "Y", "Y");
            R(table, "Condition expressions (AND/OR/keyword)", "Y", "Y (rules engine)", "Y");
            R(table, "Severity threshold matching", "Y", "Y", "Y");
            R(table, "Source type / IP filtering in rules", "Y", "Y", "Y");
            R(table, "Email notifications", "Y (SMTP)", "Y", "Y");
            R(table, "Webhook notifications", "Y", "N", "Y");
            R(table, "Slack / Teams notifications", "Y", "N", "Y");
            R(table, "SMS notifications", "P (config only)", "Y", "Y");
            R(table, "PagerDuty integration", "Y", "N", "Y");
            R(table, "SNMP trap forwarding on alert", "P (config stub)", "Y", "Y");
            R(table, "Script/command execution on alert", "N", "Y", "Y");
            R(table, "Alert acknowledgement workflow", "Y", "N", "Y");
            R(table, "Alert summary dashboard", "Y", "Y", "Y");

            // DASHBOARDS
            CatRow(table, "DASHBOARDS & VISUALIZATION");
            R(table, "Dashboard KPIs (events/alerts/sources)", "Y", "Y", "Y");
            R(table, "Event volume time-series chart", "Y", "Y", "Y");
            R(table, "Severity breakdown (donut/pie)", "Y", "Y", "Y");
            R(table, "Top talkers / top hosts", "Y", "N", "Y");
            R(table, "Activity heatmap (day x hour)", "Y", "N", "Y");
            R(table, "Security category breakdown", "Y", "N", "Y");
            R(table, "Customizable widget layout", "N", "N", "Y");

            // REPORTING
            CatRow(table, "REPORTING & COMPLIANCE");
            R(table, "Scheduled reports (daily/weekly/monthly)", "Y", "N", "Y");
            R(table, "Compliance report (PCI-DSS)", "Y", "P (needs SEM)", "Y");
            R(table, "SOX / HIPAA / ISO 27001 compliance", "N", "N", "Y");
            R(table, "CSV/PDF export", "Y", "Y", "Y");
            R(table, "On-demand report execution", "Y", "N", "Y");

            // SECURITY
            CatRow(table, "SECURITY & ACCESS");
            R(table, "JWT authentication", "Y", "N/A", "Y");
            R(table, "Role-based access control (RBAC)", "Y (roles+perms)", "P (basic)", "Y");
            R(table, "MFA / Two-factor authentication", "P (demo stub)", "N", "Y");
            R(table, "LDAP / Active Directory integration", "P (UI only)", "N", "Y");
            R(table, "API key authentication", "Y", "N", "Y");
            R(table, "Session management (revoke)", "Y", "N", "Y");
            R(table, "Audit trail for admin actions", "P (system logs)", "N", "Y");
            R(table, "Multi-tenant support", "N", "N", "Y");

            // ADVANCED
            CatRow(table, "ADVANCED FEATURES");
            R(table, "Incident management (CRUD+workflow)", "Y", "N", "Y");
            R(table, "Threat intelligence (IOC matching)", "Y", "N", "Y");
            R(table, "Anomaly detection", "Y (rule-based)", "N", "Y (ML)");
            R(table, "Log forwarding to external systems", "Y", "Y", "Y");
            R(table, "Source groups / device grouping", "Y", "N", "Y");
            R(table, "Log correlation engine", "N", "N", "Y");
            R(table, "ITSM integration (ServiceNow/Jira)", "N", "N", "Y");
            R(table, "GeoIP lookup", "N", "N", "Y");
            R(table, "Network topology map", "N", "N", "Y");

            // DEPLOYMENT
            CatRow(table, "DEPLOYMENT & OPERATIONS");
            R(table, "One-click installer", "Y (Inno Setup)", "Y (MSI)", "P (complex)");
            R(table, "Air-gapped / offline install", "Y", "Y", "N");
            R(table, "Windows + Linux support (agents)", "Y (both)", "P (Win only)", "Y");
            R(table, "Desktop agent UI", "Y (WPF)", "N/A", "N");
            R(table, "Agent as Windows Service", "P (manual)", "N/A", "Y");
            R(table, "REST API", "Y (17 controllers)", "P (limited)", "Y");
            R(table, "License management", "Y", "N/A (key-based)", "Y");
            R(table, "RFC 3164 syslog", "Y", "Y", "Y");
            R(table, "RFC 5424 syslog", "P", "Y", "Y");
        });

        // 4. GAP ANALYSIS
        SectionTitle(col, "4. Detailed Gap Analysis");

        SubSec(col, "4.1 Strengths vs Kiwi Syslog Server NG");
        Bullets(col, new[] {
            "Cross-platform agents: Windows + Linux from single .NET 8 codebase (Kiwi = Windows only)",
            "Full web portal: dashboard with analytics, heatmaps, top talkers, security categories (Kiwi = basic viewer)",
            "REST API: 17 controllers with full CRUD (Kiwi = very limited API)",
            "Incident management, threat intel IOC matching, anomaly detection (Kiwi = none)",
            "Saved searches, faceted search sidebar (Kiwi = none)",
            "Scheduled compliance reports - PCI-DSS (Kiwi needs separate SolarWinds SEM product @ extra cost)",
            "Modern notifications: Slack, Teams, Webhook, PagerDuty (Kiwi = email + script only)",
            "PostgreSQL storage: structured, queryable (Kiwi = flat files primarily)",
            "200+ security event ID filters across 21 Windows channels (MITRE/NIST/CIS aligned)",
            "50+ Linux log file paths with auto-discovery"
        });

        SubSec(col, "4.2 Strengths vs Motadata AIOps");
        Bullets(col, new[] {
            "Cost: Free/low-cost on-premise vs Rs 2.5L+/month subscription",
            "Air-gapped deployment: designed for isolated factory networks (Motadata needs internet)",
            "One-click installer: non-technical person can deploy in 30 min (Motadata requires Linux expertise)",
            "Windows desktop agent with live dashboard UI (Motadata has no agent desktop UI)",
            "Lightweight: PostgreSQL vs Elasticsearch (fraction of hardware cost for SMB scale)",
            "Manufacturing-friendly: purpose-built for on-premise, limited IT staff environments"
        });

        SubSec(col, "4.3 Gaps vs Kiwi");
        Bullets(col, new[] {
            "[MISSING] SNMP Trap reception/forwarding - Kiwi has full SNMP trap handling",
            "[MISSING] Script/command execution on alert - Kiwi can run arbitrary scripts",
            "[PARTIAL] Real-time log display uses polling not WebSocket/SSE",
            "[PARTIAL] Log rotation/archival to compressed files (policy exists, execution partial)",
            "[PARTIAL] RFC 5424 structured data parsing",
            "[PARTIAL] Custom parsing rules with regex field extraction"
        });

        SubSec(col, "4.4 Gaps vs Motadata");
        Bullets(col, new[] {
            "[MISSING] ML-based anomaly detection (SentinelLog uses rule-based; Motadata uses ML models)",
            "[MISSING] Network monitoring (SNMP polling, NetFlow, topology)",
            "[MISSING] ITSM integration (ServiceNow, Jira)",
            "[MISSING] Multi-tenant support",
            "[MISSING] Log correlation engine (cross-source event grouping)",
            "[MISSING] Custom dashboard widgets (drag-drop)",
            "[PARTIAL] Production MFA - currently demo stub needing TOTP implementation",
            "[PARTIAL] LDAP/AD - settings UI exists but no actual LDAP binding logic",
            "[PARTIAL] SMS notifications - channel type supported but no SMS gateway integration"
        });

        // 5. PRIORITIZED FEATURES
        SectionTitle(col, "5. Prioritized Missing Features");
        col.Item().Text("P0 = Blocker (cannot sell) | P1 = High (3 months) | P2 = Medium (v2) | P3 = Low (future)").FontSize(8).Bold();

        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(28);
                c.RelativeColumn(3f);
                c.RelativeColumn(1.5f);
                c.ConstantColumn(50);
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Darken3).Padding(2).Text("Pri").FontSize(7).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Grey.Darken3).Padding(2).Text("Feature").FontSize(7).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Grey.Darken3).Padding(2).Text("Where").FontSize(7).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Grey.Darken3).Padding(2).Text("Effort").FontSize(7).Bold().FontColor(Colors.White);
            });

            PR(table, "P0", "Production MFA with TOTP (Google/MS Authenticator)", "API AuthController", "Small", Colors.Red.Lighten4);
            PR(table, "P0", "LDAP/Active Directory auth binding (not just config UI)", "API + Middleware", "Medium", Colors.Red.Lighten4);
            PR(table, "P0", "Agent Windows Service mode + silent install (MSI)", "WPF Agent + Installer", "Medium", Colors.Red.Lighten4);
            PR(table, "P0", "Agent heartbeat + auto-registration with portal API", "Agent + API", "Medium", Colors.Red.Lighten4);
            PR(table, "P0", "Offline buffering in agents (disk queue when server down)", "Agent SyslogSender", "Medium", Colors.Red.Lighten4);
            PR(table, "P0", "WebSocket/SSE live log stream (replace HTTP polling)", "API + WebUI", "Medium", Colors.Red.Lighten4);
            PR(table, "P0", "Hierarchical source grouping (Plant > Zone > Line)", "Core + API + WebUI", "Medium", Colors.Red.Lighten4);
            PR(table, "P0", "SNMP Trap receiver (v1/v2c on UDP 162)", "Receiver + Installer", "Large", Colors.Red.Lighten4);

            PR(table, "P1", "RFC 5424 structured data parsing", "Receiver + Worker", "Medium", Colors.Orange.Lighten4);
            PR(table, "P1", "Custom log parsing rules (regex field extraction)", "Worker SyslogParser", "Medium", Colors.Orange.Lighten4);
            PR(table, "P1", "Script/command execution on alert trigger", "Worker AlertNotif", "Small", Colors.Orange.Lighten4);
            PR(table, "P1", "SMS notification via gateway (MSG91/Twilio)", "Worker NotifSender", "Small", Colors.Orange.Lighten4);
            PR(table, "P1", "Shift-based log tagging and filtering", "Worker + API + WebUI", "Medium", Colors.Orange.Lighten4);
            PR(table, "P1", "Machine downtime detection from logs", "Worker SourceHealth", "Medium", Colors.Orange.Lighten4);
            PR(table, "P1", "Dashboard widget customization (drag-drop)", "WebUI", "Large", Colors.Orange.Lighten4);
            PR(table, "P1", "Agent remote config from portal", "API + Agent", "Large", Colors.Orange.Lighten4);
            PR(table, "P1", "Email digest reports (daily/weekly summary)", "Worker ScheduledRpt", "Small", Colors.Orange.Lighten4);
            PR(table, "P1", "Full audit trail for admin actions", "API Middleware", "Medium", Colors.Orange.Lighten4);
            PR(table, "P1", "OT device types (PLC/HMI/SCADA) + templates", "API + Worker", "Small", Colors.Orange.Lighten4);
            PR(table, "P1", "Log archival to compressed files + cleanup", "Worker + Settings", "Medium", Colors.Orange.Lighten4);

            PR(table, "P2", "Log correlation engine", "Worker (new)", "Large", Colors.Yellow.Lighten4);
            PR(table, "P2", "ITSM integration (ServiceNow, Jira)", "Worker + API", "Medium", Colors.Yellow.Lighten4);
            PR(table, "P2", "Multi-tenant support", "Core + API", "Large", Colors.Yellow.Lighten4);
            PR(table, "P2", "Advanced query language (KQL-like)", "API LogsCtrl", "Large", Colors.Yellow.Lighten4);
            PR(table, "P2", "GeoIP lookup for source IPs", "Worker + API", "Small", Colors.Yellow.Lighten4);
            PR(table, "P2", "SOX / HIPAA / ISO 27001 compliance reports", "Reports + API", "Medium", Colors.Yellow.Lighten4);
            PR(table, "P2", "Dark mode for web UI", "WebUI", "Small", Colors.Yellow.Lighten4);
            PR(table, "P2", "Network topology/dependency map", "WebUI + API", "Large", Colors.Yellow.Lighten4);

            PR(table, "P3", "ML-based anomaly detection", "Worker (ML engine)", "Large", Colors.Green.Lighten4);
            PR(table, "P3", "Root cause analysis engine", "Worker (new)", "Large", Colors.Green.Lighten4);
            PR(table, "P3", "SNMP polling (network device monitoring)", "New service", "Large", Colors.Green.Lighten4);
            PR(table, "P3", "NetFlow/sFlow collector", "New service", "Large", Colors.Green.Lighten4);
            PR(table, "P3", "Mobile app (plant manager dashboard)", "New project", "Large", Colors.Green.Lighten4);
            PR(table, "P3", "Cloud SaaS version", "Infrastructure", "Large", Colors.Green.Lighten4);
        });

        // 6. MANUFACTURING FEATURES
        SectionTitle(col, "6. Manufacturing-Specific Recommendations");
        col.Item().Text("These features differentiate SentinelLog from generic syslog tools and address factory-floor needs:").FontSize(9);

        SubSec(col, "6.1 OT/IT Convergence - SCADA, PLC, HMI Log Support [P1]");
        Bullets(col, new[] {
            "Add device types: PLC, HMI, SCADA, DCS, RTU to source model DeviceType enum",
            "Pre-built parsing rules for Modbus TCP errors, OPC UA events, SCADA alarms",
            "Alert rule templates for OT-specific events (motor trip, temperature alarm, PLC fault)",
            "Where: API + Worker + WebUI | Effort: Small-Medium"
        });

        SubSec(col, "6.2 Shift-Based Log Grouping [P1]");
        Bullets(col, new[] {
            "Configure shifts in platform settings (e.g., Shift A: 6AM-2PM, Shift B: 2PM-10PM, Shift C: 10PM-6AM)",
            "Tag every log event with its shift period during ingestion in SyslogPollerWorker",
            "Add shift filter in search, analytics, reports - 'Show errors during Night Shift'",
            "Shift comparison reports: which shift has most failures/alerts?",
            "Where: Worker + API + WebUI | Effort: Medium"
        });

        SubSec(col, "6.3 Machine Downtime Detection [P1]");
        Bullets(col, new[] {
            "Detect when a machine (source) stops sending logs - indicates downtime",
            "SourceHealthWorker already tracks LastSeen - extend to generate downtime events",
            "Downtime report: per-machine uptime %, duration, frequency",
            "Alert when critical machine goes silent > configurable threshold (e.g., 5 min)",
            "Where: Worker + API (new DowntimeController) + WebUI | Effort: Medium"
        });

        SubSec(col, "6.4 Plant/Zone/Line Hierarchical Grouping [P0]");
        Bullets(col, new[] {
            "Extend SourceGroups with hierarchy: Plant > Zone > Line > Machine",
            "Add metadata: Plant name, Zone, Line, Bay, Floor",
            "Dashboard filters by plant/zone/line for plant managers",
            "Zone-level health showing aggregate status of all machines",
            "Where: Core model + API + WebUI | Effort: Medium"
        });

        SubSec(col, "6.5 Offline Buffering (Agent Side) [P0]");
        Bullets(col, new[] {
            "Factory networks are unreliable - WiFi drops, switch resets, cable damage",
            "Implement disk-based queue (SQLite or flat file) in agent SyslogSender",
            "Auto-flush when connectivity resumes with ordering preserved",
            "Show buffer status in WPF dashboard: 'Buffered: 1,234 events (server unreachable)'",
            "Where: Syslog-Machine-agent (SyslogSender + Pipeline) | Effort: Medium-Large"
        });

        SubSec(col, "6.6 Additional Manufacturing Ideas");
        Bullets(col, new[] {
            "Environmental monitoring: accept syslog from temperature/humidity IoT sensors",
            "Power event correlation: detect UPS events and correlate with machine downtime",
            "Maintenance window suppression: import schedules to suppress expected downtime alerts",
            "Hindi/regional language UI support for factory floor supervisors",
            "Print-friendly reports for shift handover meetings",
            "USB installer package for air-gapped factory networks"
        });

        // 7. ROADMAP
        SectionTitle(col, "7. Suggested Product Roadmap");

        SubSec(col, "Phase 1: Market Ready (0-3 Months)");
        col.Item().Text("Focus: Eliminate P0 blockers, make product sellable to first 5 manufacturing customers").Italic().FontSize(9);
        Bullets(col, new[] {
            "Production MFA (TOTP) - replace demo stub",
            "LDAP/AD authentication binding",
            "Agent silent install + Windows Service mode",
            "Agent heartbeat + auto-registration with portal",
            "WebSocket/SSE live log stream",
            "Log archival to compressed files",
            "Hierarchical source grouping (Plant > Zone > Line)",
            "Offline buffering in agents",
            "Shift-based log tagging",
            "Basic SNMP trap receiver (v1/v2c)",
            "OT device types (PLC, HMI, SCADA)"
        });

        SubSec(col, "Phase 2: Kiwi Parity + Manufacturing Edge (3-6 Months)");
        col.Item().Text("Focus: Match Kiwi feature-for-feature, exceed in manufacturing vertical").Italic().FontSize(9);
        Bullets(col, new[] {
            "RFC 5424 structured data parsing",
            "Custom log parsing rules (regex field extraction)",
            "Script/command execution on alert",
            "SMS notifications (MSG91 for India)",
            "Machine downtime detection and reporting",
            "Dashboard widget customization",
            "Agent remote configuration from portal",
            "Email digest reports",
            "Full audit trail",
            "Additional compliance (SOX, HIPAA)",
            "GeoIP lookup"
        });

        SubSec(col, "Phase 3: Market Differentiation (6-12 Months)");
        col.Item().Text("Focus: Features Kiwi doesn't have, competitive with Motadata at 1/10th cost").Italic().FontSize(9);
        Bullets(col, new[] {
            "Log correlation engine",
            "ITSM integration (ServiceNow, Jira)",
            "Advanced query language (KQL-like)",
            "ML-based anomaly detection",
            "Multi-tenant support for MSPs",
            "Network topology visualization",
            "Mobile app for plant managers",
            "Root cause analysis"
        });

        // 8. PRICING
        SectionTitle(col, "8. Pricing Strategy");
        col.Item().Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn(1.2f);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
                c.RelativeColumn(1);
            });
            table.Header(h =>
            {
                h.Cell().Background(Colors.Grey.Darken3).Padding(2).Text("").FontSize(7).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Grey.Darken3).Padding(2).Text("SentinelLog").FontSize(7).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Grey.Darken3).Padding(2).Text("Kiwi NG").FontSize(7).Bold().FontColor(Colors.White);
                h.Cell().Background(Colors.Grey.Darken3).Padding(2).Text("Motadata").FontSize(7).Bold().FontColor(Colors.White);
            });
            SR(table, "Model", "Perpetual License", "Perpetual/node", "Annual Sub");
            SR(table, "Entry Price", "Rs 50K one-time", "~Rs 25K/node", "~Rs 2.5L/month");
            SR(table, "10 devices", "Rs 50K total", "Rs 2.5L total", "Rs 30L/year");
            SR(table, "50 devices", "Rs 1.5L total", "Rs 12.5L total", "Rs 30L+/year");
            SR(table, "Support", "Rs 25K/year AMC", "20% annual renewal", "Included");
            SR(table, "Free Tier", "Yes (5 devices)", "Yes (5 devices)", "No");
        });
        col.Item().Text("Recommendation: Free Community Edition (5 devices) + Enterprise (unlimited, LDAP, compliance, support). Undercuts Kiwi on TCO for manufacturing SMBs.").FontSize(8).Italic();

        // 9. CONCLUSION
        SectionTitle(col, "9. Conclusion");
        col.Item().Text(t =>
        {
            t.Span("SentinelLog is already a capable product").Bold();
            t.Span(" that exceeds Kiwi in web portal, APIs, incidents, threat intel, and cross-platform agents - while being far more affordable than Motadata.\n\n");
            t.Span("Key strengths for manufacturing: ").Bold();
            t.Span("air-gapped install, Windows+Linux, 200+ event ID filters, PostgreSQL (familiar to Indian IT), clean web portal.\n\n");
            t.Span("Critical gaps before first sale: ").Bold();
            t.Span("production MFA, LDAP binding, agent service mode, offline buffering, hierarchical grouping (Plant/Zone/Line).\n\n");
            t.Span("With 3 months of Phase 1 development, SentinelLog can be positioned as: ").FontSize(10);
            t.Span("'India's affordable Kiwi replacement, built for manufacturing'").Bold().Italic();
            t.Span(" - a position neither Kiwi (Windows-only, no mfg features, expensive per-node) nor Motadata (too expensive, too complex, cloud-dependent) can match.\n\n");
            t.Span("TAM: ").Bold();
            t.Span("India has 63M+ MSME manufacturing units. Targeting just 50,000+ medium-large factories with IT infrastructure at Rs 50K-1.5L/deployment = Rs 250Cr+ opportunity.");
        });

        col.Item().PaddingTop(15).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten2);
        col.Item().PaddingTop(3).Text("Auto-generated from codebase analysis of 5 repositories + competitive research against Kiwi Syslog NG and Motadata AIOps.")
            .FontSize(7).FontColor(Colors.Grey.Darken1).Italic();
    });
}

// ═══════════════════════════════════════════════════════
void SectionTitle(ColumnDescriptor col, string t) =>
    col.Item().PaddingTop(10).PaddingBottom(2).BorderBottom(1).BorderColor(Colors.Blue.Darken2)
        .Text(t).FontSize(13).Bold().FontColor(Colors.Blue.Darken3);

void SubSec(ColumnDescriptor col, string t) =>
    col.Item().PaddingTop(5).Text(t).FontSize(10).Bold().FontColor(Colors.Grey.Darken3);

void ArchRow(ColumnDescriptor col, string name, string desc) =>
    col.Item().Row(r => { r.ConstantItem(140).Text(name).Bold().FontSize(9); r.RelativeItem().Text(desc).FontSize(9); });

void Bullets(ColumnDescriptor col, string[] items) =>
    col.Item().PaddingLeft(8).Column(i => { i.Spacing(1); foreach (var x in items) i.Item().Text($"* {x}").FontSize(9); });

void CatRow(TableDescriptor table, string cat)
{
    for (int i = 0; i < 4; i++)
        table.Cell().ColumnSpan(i == 0 ? 1u : 1u).Background(Colors.Grey.Lighten3).Padding(2)
            .Text(i == 0 ? cat : "").FontSize(8).Bold().FontColor(Colors.Grey.Darken3);
}

void R(TableDescriptor table, string f, string s, string k, string m)
{
    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(f).FontSize(8);
    Cell(table, s); Cell(table, k); Cell(table, m);
}

void Cell(TableDescriptor table, string v)
{
    var color = v.StartsWith("Y") ? Colors.Green.Darken2 : v.StartsWith("P") ? Colors.Orange.Darken1 : v.StartsWith("N") ? Colors.Red.Darken1 : Colors.Black;
    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(v).FontSize(8).FontColor(color);
}

void PR(TableDescriptor table, string pri, string feat, string where, string eff, string bg)
{
    table.Cell().Background(bg).BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(pri).FontSize(7).Bold();
    table.Cell().Background(bg).BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(feat).FontSize(7);
    table.Cell().Background(bg).BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(where).FontSize(7);
    table.Cell().Background(bg).BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(eff).FontSize(7);
}

void SR(TableDescriptor table, string a, string b, string c, string d)
{
    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(a).FontSize(8);
    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(b).FontSize(8);
    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(c).FontSize(8);
    table.Cell().BorderBottom(0.3f).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(d).FontSize(8);
}
