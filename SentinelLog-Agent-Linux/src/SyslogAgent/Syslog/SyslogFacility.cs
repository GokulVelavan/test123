namespace SyslogAgent.Syslog;

/// <summary>
/// Syslog Facility codes per RFC 3164
/// </summary>
public enum SyslogFacility
{
    Kernel = 0,
    UserLevel = 1,
    Mail = 2,
    System = 3,
    Security = 4,
    Syslogd = 5,
    LinePrinter = 6,
    News = 7,
    Uucp = 8,
    Clock = 9,
    AuthPriv = 10,
    Ftp = 11,
    Ntp = 12,
    Audit = 13,
    Alert = 14,
    Cron = 15,
    Local0 = 16,
    Local1 = 17,
    Local2 = 18,
    Local3 = 19,
    Local4 = 20,
    Local5 = 21,
    Local6 = 22,
    Local7 = 23
}
