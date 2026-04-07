$ErrorActionPreference = "Continue"
$log = "D:\SyslogAgent-Desktop\filter-test-result.txt"

if (-not [System.Diagnostics.EventLog]::SourceExists("SyslogAgentTest")) {
    New-EventLog -LogName Application -Source SyslogAgentTest
}

# === EVENT IN FILTER ===
# Application Event ID 1000 is in the filter list
Write-EventLog -LogName Application -Source SyslogAgentTest -EventId 1000 -EntryType Error `
    -Message "FILTER-TEST-IN: This event ID 1000 IS in the filter - should appear"

# === EVENT NOT IN FILTER ===
# Application Event ID 9999 is NOT in the filter list
Write-EventLog -LogName Application -Source SyslogAgentTest -EventId 9999 -EntryType Information `
    -Message "FILTER-TEST-OUT: This event ID 9999 is NOT in the filter - should be blocked"

"Sent both events at $(Get-Date -Format 'HH:mm:ss')" | Out-File -FilePath $log -Encoding UTF8
"  IN filter:  Application EventID 1000 - message contains FILTER-TEST-IN" | Out-File -FilePath $log -Append -Encoding UTF8
"  NOT filter: Application EventID 9999 - message contains FILTER-TEST-OUT" | Out-File -FilePath $log -Append -Encoding UTF8
