<#
.SYNOPSIS
    Configures Windows Firewall rules for Syslog-NG and PostgreSQL.
#>
param(
    [int]$SyslogPort = 514,
    [int]$TLSPort = 6514,
    [int]$PGPort  = 5432
)

$ErrorActionPreference = "Continue"

$rules = @(
    @{ Name = "Syslog-NG UDP (Port $SyslogPort)";  Port = $SyslogPort; Protocol = "UDP" },
    @{ Name = "Syslog-NG TCP (Port $SyslogPort)";  Port = $SyslogPort; Protocol = "TCP" },
    @{ Name = "Syslog-NG TLS (Port $TLSPort)";     Port = $TLSPort;    Protocol = "TCP" },
    @{ Name = "PostgreSQL (Port $PGPort)";          Port = $PGPort;     Protocol = "TCP" }
)

foreach ($rule in $rules) {
    $existing = Get-NetFirewallRule -DisplayName $rule.Name -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "  Firewall rule already exists: $($rule.Name)" -ForegroundColor Gray
        $existing | Get-NetFirewallPortFilter | Set-NetFirewallPortFilter -LocalPort $rule.Port -Protocol $rule.Protocol
    } else {
        New-NetFirewallRule `
            -DisplayName $rule.Name `
            -Direction Inbound `
            -Action Allow `
            -Protocol $rule.Protocol `
            -LocalPort $rule.Port `
            -Profile Any `
            -Description "Created by Syslog Stack Installer" | Out-Null
        Write-Host "  Firewall rule created: $($rule.Name)" -ForegroundColor Green
    }
}

Write-Host "  Firewall configuration complete." -ForegroundColor Green
