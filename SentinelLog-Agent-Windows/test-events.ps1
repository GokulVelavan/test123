# ============================================================
# SyslogAgent Windows Event Log Test Script
# Generates test events across configured channels
# Must be run as Administrator
# ============================================================

$ErrorActionPreference = "Continue"
$results = @()
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$logFile = "D:\SyslogAgent-Desktop\test-results.txt"

function Log($msg) {
    $msg | Out-File -FilePath $logFile -Append -Encoding UTF8
    Write-Host $msg
}

# Clear previous results
"" | Out-File -FilePath $logFile -Encoding UTF8

Log "========================================"
Log " SyslogAgent Event Generation Test"
Log " Started: $timestamp"
Log "========================================"
Log ""

function Test-Event {
    param([string]$Channel, [string]$Description, [scriptblock]$Action)

    try {
        & $Action
        $status = "OK"
        Log "[$Channel] $Description ... GENERATED"
    }
    catch {
        $status = "FAILED: $($_.Exception.Message)"
        Log "[$Channel] $Description ... FAILED: $($_.Exception.Message)"
    }
    return [PSCustomObject]@{ Channel=$Channel; Test=$Description; Status=$status }
}

# ============================================================
# 1. APPLICATION LOG - Event IDs: 1000, 1001, 1026
# ============================================================
Log ""
Log "--- Application Log ---"

$source = "SyslogAgentTest"
if (-not [System.Diagnostics.EventLog]::SourceExists($source)) {
    New-EventLog -LogName Application -Source $source
}

$results += Test-Event "Application" "Error event ID 1000 (app crash)" {
    Write-EventLog -LogName Application -Source $source -EventId 1000 -EntryType Error `
        -Message "TEST: Simulated application error - SyslogAgent test at $timestamp"
}

$results += Test-Event "Application" "Warning event ID 1001 (app issue)" {
    Write-EventLog -LogName Application -Source $source -EventId 1001 -EntryType Warning `
        -Message "TEST: Simulated application warning - SyslogAgent test at $timestamp"
}

$results += Test-Event "Application" "Error event ID 1026 (.NET runtime)" {
    Write-EventLog -LogName Application -Source $source -EventId 1026 -EntryType Error `
        -Message "TEST: Simulated .NET runtime error - SyslogAgent test at $timestamp"
}

# ============================================================
# 2. SYSTEM LOG - Event IDs: 7035, 7036
# ============================================================
Log ""
Log "--- System Log ---"

$results += Test-Event "System" "Stop/Start Spooler (triggers 7035/7036)" {
    Stop-Service -Name Spooler -Force
    Start-Sleep -Seconds 1
    Start-Service -Name Spooler
}

$results += Test-Event "System" "Restart W32Time (triggers 7035/7036)" {
    Restart-Service -Name W32Time -Force
}

# ============================================================
# 3. SECURITY LOG - Event IDs: 4624, 4625, 4648, 4672
# ============================================================
Log ""
Log "--- Security Log ---"

$results += Test-Event "Security" "Failed logon (triggers 4625/4648)" {
    $null = net use \\127.0.0.1\IPC$ /user:FAKE_TEST_USER "WrongPassword123!" 2>&1
}

$results += Test-Event "Security" "whoami /priv (triggers 4624/4672)" {
    $null = whoami /priv 2>&1
}

# ============================================================
# 4. POWERSHELL OPERATIONAL - Event IDs: 4103, 4104
# ============================================================
Log ""
Log "--- PowerShell Operational ---"

$results += Test-Event "PowerShell" "Script block execution (triggers 4104)" {
    $sb = [scriptblock]::Create('Write-Output "SyslogAgent-Test-ScriptBlock"')
    & $sb | Out-Null
}

$results += Test-Event "PowerShell" "Import module (triggers 4103)" {
    Import-Module Microsoft.PowerShell.Management -Force
}

# ============================================================
# 5. TASK SCHEDULER - Event IDs: 106, 140, 141, 200, 201
# ============================================================
Log ""
Log "--- Task Scheduler Operational ---"

$results += Test-Event "TaskScheduler" "Create/run/delete task (106/200/201/141)" {
    $action = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c echo SyslogAgent Test"
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddHours(1)
    Register-ScheduledTask -TaskName "SyslogAgentTest" -Action $action -Trigger $trigger -Force | Out-Null
    Start-Sleep -Seconds 1
    Start-ScheduledTask -TaskName "SyslogAgentTest"
    Start-Sleep -Seconds 2
    Unregister-ScheduledTask -TaskName "SyslogAgentTest" -Confirm:$false
}

# ============================================================
# 6. WINDOWS DEFENDER - Event IDs: 1116, 1117
# ============================================================
Log ""
Log "--- Windows Defender Operational ---"

$results += Test-Event "Defender" "EICAR test file (triggers 1116/1117)" {
    $testDir = "$env:TEMP\SyslogAgentTest"
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    $eicar = 'X5O!P%@AP[4\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*'
    try {
        Set-Content -Path "$testDir\eicar.txt" -Value $eicar -ErrorAction Stop
        Start-Sleep -Seconds 3
    } catch { }
    Remove-Item -Path $testDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ============================================================
# 7. FIREWALL - Event IDs: 2004, 2005, 2006
# ============================================================
Log ""
Log "--- Windows Firewall ---"

$results += Test-Event "Firewall" "Add/modify/remove rule (2004/2005/2006)" {
    New-NetFirewallRule -DisplayName "SyslogAgentTest" -Direction Inbound -Action Block -Protocol TCP -LocalPort 59999 | Out-Null
    Start-Sleep -Seconds 1
    Set-NetFirewallRule -DisplayName "SyslogAgentTest" -Action Allow
    Start-Sleep -Seconds 1
    Remove-NetFirewallRule -DisplayName "SyslogAgentTest"
}

# ============================================================
# 8. SYSMON (if installed) - Event IDs: 1, 3, 11, 22
# ============================================================
Log ""
Log "--- Sysmon ---"

$sysmonRunning = Get-Service -Name Sysmon* -ErrorAction SilentlyContinue
if ($sysmonRunning) {
    $results += Test-Event "Sysmon" "Launch process (ID 1 - Process Create)" {
        $p = Start-Process -FilePath "cmd.exe" -ArgumentList "/c echo SyslogAgent Sysmon Test" -PassThru -WindowStyle Hidden
        $p.WaitForExit(5000) | Out-Null
    }

    $results += Test-Event "Sysmon" "Create file (ID 11 - File Create)" {
        $tempFile = "$env:TEMP\SyslogAgentSysmonTest.txt"
        Set-Content -Path $tempFile -Value "Sysmon file creation test"
        Remove-Item -Path $tempFile -Force
    }

    $results += Test-Event "Sysmon" "DNS query (ID 22 - DNS Query)" {
        Resolve-DnsName -Name "syslogagent-test.example.com" -ErrorAction SilentlyContinue | Out-Null
    }
} else {
    Log "[Sysmon] NOT INSTALLED - skipping"
    $results += [PSCustomObject]@{ Channel="Sysmon"; Test="Not installed"; Status="SKIPPED" }
}

# ============================================================
# 9. DNS CLIENT - Event IDs: 3006, 3008, 3020
# ============================================================
Log ""
Log "--- DNS Client Operational ---"

$results += Test-Event "DNS-Client" "Successful DNS query (3006/3020)" {
    Resolve-DnsName -Name "www.google.com" -ErrorAction SilentlyContinue | Out-Null
}

$results += Test-Event "DNS-Client" "Failed DNS query (3008)" {
    Resolve-DnsName -Name "this-domain-does-not-exist-syslogtest.invalid" -ErrorAction SilentlyContinue | Out-Null
}

# ============================================================
# 10. RDP / TERMINAL SERVICES
# ============================================================
Log ""
Log "--- Terminal Services / RDP ---"

$results += Test-Event "RDP" "Query RDP operational log" {
    $rdpService = Get-Service -Name TermService -ErrorAction SilentlyContinue
    if ($rdpService -and $rdpService.Status -eq 'Running') {
        Log "  (RDP service is running)"
    } else {
        Log "  (RDP service not running)"
    }
    Get-WinEvent -LogName "Microsoft-Windows-TerminalServices-LocalSessionManager/Operational" -MaxEvents 1 -ErrorAction Stop | Out-Null
}

# ============================================================
# 11. WMI ACTIVITY - Event IDs: 5857, 5858
# ============================================================
Log ""
Log "--- WMI Activity ---"

$results += Test-Event "WMI" "WMI query (triggers 5857 provider load)" {
    Get-WmiObject -Class Win32_OperatingSystem | Out-Null
}

# ============================================================
# 12. BITS CLIENT - Event IDs: 3, 4, 59, 60
# ============================================================
Log ""
Log "--- BITS Client ---"

$results += Test-Event "BITS" "BITS transfer (triggers 3/4/59/60)" {
    $bitsJob = Start-BitsTransfer -Source "https://www.google.com/robots.txt" `
        -Destination "$env:TEMP\SyslogAgentBitsTest.txt" -Asynchronous
    $bitsJob | Complete-BitsTransfer
    Remove-Item -Path "$env:TEMP\SyslogAgentBitsTest.txt" -Force -ErrorAction SilentlyContinue
}

# ============================================================
# SUMMARY
# ============================================================
Log ""
Log "========================================"
Log " TEST RESULTS SUMMARY"
Log "========================================"

$ok = ($results | Where-Object { $_.Status -eq "OK" }).Count
$failed = ($results | Where-Object { $_.Status -like "FAILED*" }).Count
$skipped = ($results | Where-Object { $_.Status -eq "SKIPPED" }).Count

foreach ($r in $results) {
    $mark = if ($r.Status -eq "OK") { "[PASS]" } elseif ($r.Status -eq "SKIPPED") { "[SKIP]" } else { "[FAIL]" }
    Log "$mark $($r.Channel): $($r.Test)"
    if ($r.Status -like "FAILED*") {
        Log "       $($r.Status)"
    }
}

Log ""
Log "Total: $($results.Count) | OK: $ok | Failed: $failed | Skipped: $skipped"
Log ""
Log "Check your SyslogAgent Dashboard to verify these events were captured."
Log "Events should appear within BatchIntervalSeconds (60s) from appsettings.json."
