<#
.SYNOPSIS
    Uninstalls the Syslog-NG stack (stops containers, cleans up state).
#>
param(
    [string]$InstallDir = "C:\SyslogStack",
    [switch]$RemovePostgres,
    [switch]$RemoveDocker,
    [switch]$RemoveData
)

$ErrorActionPreference = "Continue"

Write-Host "Syslog Stack Uninstaller" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host "Install dir: $InstallDir"

# Clean up scheduled tasks
schtasks /delete /tn "SyslogStackResume" /f 2>$null

# Clean up RunOnce
try {
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" `
        -Name "SyslogStackResume" -ErrorAction SilentlyContinue
} catch {}

# Clean up resume EXE
$resumeExe = Join-Path $InstallDir "SyslogStack-Resume.exe"
if (Test-Path $resumeExe) {
    Remove-Item $resumeExe -Force -ErrorAction SilentlyContinue
}

# Clean up registry
reg delete "HKLM\Software\SyslogStack" /f 2>$null

# Stop and remove container
Write-Host "Stopping syslog-ng container..."
try {
    & docker stop syslog-ng 2>$null
    & docker rm syslog-ng 2>$null
} catch {}

$payloadDir = Join-Path $InstallDir "payload"
if (Test-Path "$payloadDir\docker-compose.yml") {
    try {
        Push-Location $payloadDir
        & docker compose down 2>&1 | ForEach-Object { Write-Host "  $_" -ForegroundColor Gray }
        Pop-Location
    } catch {}
}

# Remove Docker image
Write-Host "Removing syslog-ng Docker image..."
& docker rmi syslog-ng:latest 2>$null

# Remove firewall rules
Write-Host "Removing firewall rules..."
try {
    Get-NetFirewallRule -ErrorAction SilentlyContinue |
        Where-Object { $_.Description -eq "Created by Syslog Stack Installer" } |
        ForEach-Object {
            Remove-NetFirewallRule -Name $_.Name -ErrorAction SilentlyContinue
            Write-Host "  Removed: $($_.DisplayName)" -ForegroundColor Gray
        }
} catch {}

if ($RemovePostgres) {
    Write-Host "Stopping PostgreSQL service..."
    Stop-Service -Name "postgresql-syslog" -ErrorAction SilentlyContinue
    $pgUninstaller = Join-Path $InstallDir "pgsql\uninstall-postgresql.exe"
    if (Test-Path $pgUninstaller) {
        Write-Host "Uninstalling PostgreSQL..."
        Start-Process -FilePath $pgUninstaller -ArgumentList "--mode", "unattended" -Wait
    }
}

if ($RemoveData) {
    Write-Host "Removing installation data..."
    Remove-Item -Path $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Uninstallation complete." -ForegroundColor Green
