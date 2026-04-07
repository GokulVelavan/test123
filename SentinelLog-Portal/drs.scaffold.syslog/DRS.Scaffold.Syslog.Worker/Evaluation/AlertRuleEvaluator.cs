using DRS.Scaffold.Syslog.Core.Models;

namespace DRS.Scaffold.Syslog.Worker.Evaluation;

/// <summary>
/// Evaluates a <see cref="LogEvent"/> against every active <see cref="AlertRule"/>
/// and returns the set of rules that match.
/// </summary>
public static class AlertRuleEvaluator
{
    /// <summary>
    /// Returns every rule in <paramref name="rules"/> whose condition is satisfied
    /// by <paramref name="logEvent"/>.
    /// </summary>
    public static IEnumerable<AlertRule> Evaluate(LogEvent logEvent, IEnumerable<AlertRule> rules)
    {
        foreach (var rule in rules)
        {
            if (!rule.Enabled) continue;
            if (Matches(logEvent, rule))
                yield return rule;
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static bool Matches(LogEvent evt, AlertRule rule)
    {
        // 1. Source filter — matches source IP, hostname, or device-type category
        if (!string.IsNullOrWhiteSpace(rule.SourceType))
        {
            var filter = rule.SourceType.Trim();
            var matchesIp       = !string.IsNullOrWhiteSpace(evt.SourceIp)   && string.Equals(evt.SourceIp,   filter, StringComparison.OrdinalIgnoreCase);
            var matchesHostname = !string.IsNullOrWhiteSpace(evt.Hostname)   && string.Equals(evt.Hostname,   filter, StringComparison.OrdinalIgnoreCase);
            var matchesType     = !string.IsNullOrWhiteSpace(evt.DeviceType) && string.Equals(evt.DeviceType, filter, StringComparison.OrdinalIgnoreCase);
            if (!matchesIp && !matchesHostname && !matchesType)
                return false;
        }

        // 2. Severity threshold filter
        //    Rule severity is a human label (Critical / Error / Warning / Notice / Info).
        //    LogEvent severity is an RFC-5424 numeric (0=Emergency .. 7=Debug).
        //    A rule fires when the event severity is AT LEAST as severe as the threshold.
        if (evt.Severity.HasValue && !string.IsNullOrWhiteSpace(rule.Severity))
        {
            var threshold = SeverityLabelToNumeric(rule.Severity);
            if (threshold.HasValue && evt.Severity.Value > threshold.Value)
                return false; // event is less severe than the rule threshold
        }

        // 3. ConditionExpression keyword match (simple contains check)
        if (!string.IsNullOrWhiteSpace(rule.ConditionExpression))
        {
            var msg = evt.Message ?? string.Empty;
            if (!EvaluateExpression(msg, rule.ConditionExpression))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Very simple expression evaluator.
    /// Supports:
    ///   - Plain keyword  ?  message contains keyword (case-insensitive)
    ///   - A AND B        ?  both keywords present
    ///   - A OR B         ?  either keyword present
    /// </summary>
    private static bool EvaluateExpression(string message, string expression)
    {
        if (expression.Contains(" AND ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = expression.Split(" AND ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.All(p => message.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        if (expression.Contains(" OR ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = expression.Split(" OR ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return parts.Any(p => message.Contains(p, StringComparison.OrdinalIgnoreCase));
        }

        return message.Contains(expression.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Converts a human-readable severity label to its RFC-5424 numeric equivalent.</summary>
    private static short? SeverityLabelToNumeric(string label) => label.ToLowerInvariant() switch
    {
        "emergency" or "emerg"       => 0,
        "alert"                      => 1,
        "critical" or "crit"         => 2,
        "error"    or "err"          => 3,
        "warning"  or "warn"         => 4,
        "notice"                     => 5,
        "info"     or "information"  => 6,
        "debug"                      => 7,
        _ => null
    };
}
