$ErrorActionPreference = "Continue"
$count = 0
$log = "D:\SyslogAgent-Desktop\verify-count.txt"

# Ensure test source
if (-not [System.Diagnostics.EventLog]::SourceExists("SyslogAgentTest")) {
    New-EventLog -LogName Application -Source SyslogAgentTest
}

# 1-3: Application (3 events)
Write-EventLog -LogName Application -Source SyslogAgentTest -EventId 1000 -EntryType Error -Message "VERIFY-1: Application Error at $(Get-Date)"; $count++
Write-EventLog -LogName Application -Source SyslogAgentTest -EventId 1001 -EntryType Warning -Message "VERIFY-2: Application Warning at $(Get-Date)"; $count++
Write-EventLog -LogName Application -Source SyslogAgentTest -EventId 1026 -EntryType Error -Message "VERIFY-3: .NET Runtime Error at $(Get-Date)"; $count++

# 4-5: System - service restarts (generates 7035/7036)
Restart-Service -Name Spooler -Force; $count++
Restart-Service -Name W32Time -Force; $count++

# 6: Security - failed logon (generates 4625/4648)
$null = net use \\127.0.0.1\IPC$ /user:VERIFY_USER "WrongPass!" 2>&1; $count++

# 7-8: Firewall - add + remove (generates 2004/2006)
New-NetFirewallRule -DisplayName "VerifyTest" -Direction Inbound -Action Block -Protocol TCP -LocalPort 59997 | Out-Null; $count++
Start-Sleep -Seconds 1
Remove-NetFirewallRule -DisplayName "VerifyTest"; $count++

# 9-10: Task Scheduler - create + delete (generates 106/141)
$a = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c echo verify"
$t = New-ScheduledTaskTrigger -Once -At (Get-Date).AddHours(1)
Register-ScheduledTask -TaskName "VerifyTest" -Action $a -Trigger $t -Force | Out-Null; $count++
Start-Sleep -Seconds 1
Unregister-ScheduledTask -TaskName "VerifyTest" -Confirm:$false; $count++

# 11-12: DNS queries (generates 3006/3008)
Resolve-DnsName "www.microsoft.com" -ErrorAction SilentlyContinue | Out-Null; $count++
Resolve-DnsName "bad-verify-test.invalid" -ErrorAction SilentlyContinue | Out-Null; $count++

# 13: WMI query (generates 5857)
Get-WmiObject -Class Win32_ComputerSystem | Out-Null; $count++

"Sent $count test actions at $(Get-Date -Format 'HH:mm:ss')" | Out-File -FilePath $log -Encoding UTF8
