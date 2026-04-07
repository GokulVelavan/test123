<#
.SYNOPSIS
    Installs Docker Desktop, PostgreSQL, syslog-ng container, firewall rules.
.DESCRIPTION
    Called by Inno Setup after file extraction. Single-phase: does everything
    in one run. Windows feature enablement is handled by Setup.iss via DISM.
#>
param(
    [Parameter(Mandatory)][string]$InstallDir,
    [string]$PGHost         = "localhost",
    [string]$PGPort         = "5432",
    [string]$PGDatabase     = "syslogdb",
    [string]$PGUser         = "sysloguser",
    [string]$PGPassword     = "Syslog@123",
    [string]$PGSuperPassword = "Deliverain@123$",
    [string]$SyslogPort     = "514",
    [string]$TLSPort        = "6514",
    [switch]$InstallPostgres,
    [switch]$InstallDocker,
    [switch]$GenerateTLS
)

$ErrorActionPreference = "Continue"
$ProgressPreference    = "SilentlyContinue"

# ======================== LOGGING ========================
$LogFile = Join-Path $InstallDir "install.log"
New-Item -ItemType Directory -Path $InstallDir -Force -ErrorAction SilentlyContinue | Out-Null
"" | Out-File -FilePath $LogFile -Force -ErrorAction SilentlyContinue

function Log {
    param([string]$Msg, [string]$Level = "INFO")
    $line = "[$(Get-Date -Format 'HH:mm:ss')] [$Level] $Msg"
    Add-Content -Path $LogFile -Value $line -ErrorAction SilentlyContinue
    switch ($Level) {
        "ERROR" { Write-Host $line -ForegroundColor Red }
        "WARN"  { Write-Host $line -ForegroundColor Yellow }
        "OK"    { Write-Host $line -ForegroundColor Green }
        default { Write-Host $line -ForegroundColor Gray }
    }
}

# ======================== DIAGNOSTICS ========================
function Write-Diagnostics {
    Log "======================================"
    Log "  Syslog Stack Installer v1.0.0"
    Log "  $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    Log "======================================"
    try {
        $os = Get-CimInstance Win32_OperatingSystem
        Log "OS:       $($os.Caption) (Build $($os.BuildNumber))"
        Log "Computer: $env:COMPUTERNAME"
        Log "User:     $([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)"
        $disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
        Log "Disk C:   $([math]::Round($disk.FreeSpace/1GB,1)) GB free"
    } catch {
        Log "Could not gather diagnostics: $($_.Exception.Message)" "WARN"
    }
    Log "InstallDir:    $InstallDir"
    Log "PGHost:        $PGHost"
    Log "SyslogPort:    $SyslogPort"
    Log "TLSPort:       $TLSPort"
    Log "InstallDocker: $InstallDocker"
    Log "InstallPG:     $InstallPostgres"
    Log "GenerateTLS:   $GenerateTLS"
    Log "======================================"
}

# ======================== FIND PSQL ========================
function Find-Psql {
    # Check PATH first
    $cmd = Get-Command psql -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    # Search common locations
    $paths = @(
        (Join-Path $InstallDir "pgsql\bin\psql.exe")
    )

    # Discover from running PostgreSQL service binary
    $svcs = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue
    foreach ($svc in $svcs) {
        try {
            $imgPath = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Services\$($svc.Name)" -ErrorAction SilentlyContinue).ImagePath
            if ($imgPath -match '"([^"]+)"') {
                $binDir = Split-Path $Matches[1]
                $paths = @((Join-Path $binDir "psql.exe")) + $paths
            }
        } catch {}
    }

    # Standard install locations
    $paths += @(
        "C:\Program Files\PostgreSQL\17\bin\psql.exe",
        "C:\Program Files\PostgreSQL\16\bin\psql.exe",
        "C:\Program Files\PostgreSQL\15\bin\psql.exe",
        "D:\Program Files\PostgreSQL\17\bin\psql.exe",
        "D:\Program Files\PostgreSQL\16\bin\psql.exe"
    )

    foreach ($p in $paths) {
        if (Test-Path $p) {
            Log "Found psql: $p"
            return $p
        }
    }
    return $null
}

# ======================== STEP 1: DOCKER DESKTOP ========================
function Step-InstallDocker {
    Log "=== Step 1: Docker Desktop ==="

    # Skip if already available
    $dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
    if ($dockerCmd) {
        Log "Docker CLI found: $($dockerCmd.Source)" "OK"
        Step-StartDocker
        return
    }

    if (-not $InstallDocker) {
        Log "Docker not requested and not found - skipping" "WARN"
        return
    }

    $installer = Join-Path $InstallDir "dependencies\DockerDesktop-x64.exe"
    if (-not (Test-Path $installer)) {
        throw "Docker installer not found: $installer"
    }

    $sizeMB = [math]::Round((Get-Item $installer).Length / 1MB)
    Log "Installing Docker Desktop (${sizeMB} MB) - this takes 3-5 minutes..."

    # Determine backend based on OS build
    $osBuild = [int](Get-CimInstance Win32_OperatingSystem).BuildNumber
    $backend = if ($osBuild -lt 19041) { "hyper-v" } else { "wsl-2" }
    Log "Docker backend: $backend (OS Build $osBuild)"

    $proc = Start-Process -FilePath $installer `
        -ArgumentList "install", "--quiet", "--accept-license", "--backend=$backend" `
        -Wait -PassThru -NoNewWindow
    Log "Docker installer exit code: $($proc.ExitCode)"

    if ($proc.ExitCode -notin @(0, 3, 3010)) {
        Log "Docker install returned $($proc.ExitCode) - continuing anyway" "WARN"
    }

    # Add user to docker-users group
    try {
        $user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        net localgroup docker-users $user /add 2>&1 | Out-Null
        net localgroup docker-users Administrator /add 2>&1 | Out-Null
        Log "Added user to docker-users group" "OK"
    } catch {
        Log "Could not add user to docker-users: $($_.Exception.Message)" "WARN"
    }

    # Add Docker to PATH for this session
    @("$env:ProgramFiles\Docker\Docker\resources\bin",
      "$env:ProgramFiles\Docker\Docker") | ForEach-Object {
        if (Test-Path $_) { $env:PATH = "$_;$env:PATH" }
    }

    Step-StartDocker
}

# ======================== START DOCKER DAEMON ========================
function Step-StartDocker {
    Log "Starting Docker Desktop..."

    # Find Docker Desktop.exe
    $exe = $null
    foreach ($p in @(
        "$env:ProgramFiles\Docker\Docker\Docker Desktop.exe",
        "${env:ProgramFiles(x86)}\Docker\Docker\Docker Desktop.exe",
        "$env:LOCALAPPDATA\Docker\Docker Desktop.exe"
    )) {
        if (Test-Path $p) { $exe = $p; break }
    }
    if (-not $exe) {
        throw "Docker Desktop.exe not found in standard locations."
    }

    # Launch if not running
    if (-not (Get-Process "Docker Desktop" -ErrorAction SilentlyContinue)) {
        Start-Process $exe -WindowStyle Minimized
        Log "Docker Desktop launched, waiting for initialization..."
        Start-Sleep -Seconds 15
    } else {
        Log "Docker Desktop already running" "OK"
    }

    # Wait for daemon
    Log "Waiting for Docker daemon (up to 5 minutes)..."
    $maxWait = 60  # 60 * 5s = 300s
    for ($i = 1; $i -le $maxWait; $i++) {
        try {
            $null = & docker info 2>&1
            if ($LASTEXITCODE -eq 0) {
                $ver = (& docker --version 2>&1) -join ""
                Log "Docker ready: $ver" "OK"
                return
            }
        } catch {}

        if ($i % 12 -eq 0) {
            Log "  Still waiting... ($($i*5)s elapsed)" "WARN"
            # Restart Docker Desktop if it died
            if (-not (Get-Process "Docker Desktop" -ErrorAction SilentlyContinue)) {
                Log "  Docker Desktop process not found, relaunching..." "WARN"
                Start-Process $exe -WindowStyle Minimized
                Start-Sleep -Seconds 10
            }
        }
        Start-Sleep -Seconds 5
    }
    throw "Docker daemon did not start within 5 minutes. Try starting Docker Desktop manually and re-run the installer."
}

# ======================== STEP 2: POSTGRESQL ========================
function Step-InstallPostgres {
    Log "=== Step 2: PostgreSQL ==="

    if (-not $InstallPostgres) {
        Log "Using existing PostgreSQL at ${PGHost}:${PGPort}" "OK"
        return
    }

    $pgExe = Get-ChildItem -Path (Join-Path $InstallDir "dependencies") `
        -Filter "postgresql-*.exe" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $pgExe) {
        throw "PostgreSQL installer not found in dependencies folder."
    }

    $pgInstallDir = Join-Path $InstallDir "pgsql"
    $pgDataDir    = Join-Path $InstallDir "pgdata"

    $sizeMB = [math]::Round($pgExe.Length / 1MB)
    Log "Installing PostgreSQL (${sizeMB} MB) to $pgInstallDir..."

    $proc = Start-Process -FilePath $pgExe.FullName -ArgumentList @(
        "--mode", "unattended",
        "--unattendedmodeui", "none",
        "--prefix", $pgInstallDir,
        "--datadir", $pgDataDir,
        "--superpassword", $PGSuperPassword,
        "--serverport", $PGPort,
        "--servicename", "postgresql-syslog",
        "--install_runtimes", "1",
        "--disable-components", "stackbuilder"
    ) -Wait -PassThru -NoNewWindow
    Log "PostgreSQL installer exit code: $($proc.ExitCode)"

    if ($proc.ExitCode -ne 0) {
        throw "PostgreSQL installation failed (exit code $($proc.ExitCode))."
    }
    Log "PostgreSQL installed" "OK"

    # Add psql to PATH
    $binPath = Join-Path $pgInstallDir "bin"
    if (Test-Path $binPath) { $env:PATH = "$binPath;$env:PATH" }

    # Configure for Docker access
    $pgConfFile = Join-Path $pgDataDir "postgresql.conf"
    $pgHbaFile  = Join-Path $pgDataDir "pg_hba.conf"

    if (Test-Path $pgConfFile) {
        $conf = Get-Content $pgConfFile -Raw
        if ($conf -match "listen_addresses\s*=\s*'localhost'") {
            $conf = $conf -replace "listen_addresses\s*=\s*'localhost'", "listen_addresses = '*'"
            $conf | Set-Content $pgConfFile -Encoding UTF8 -Force
            Log "postgresql.conf: listen_addresses = '*'" "OK"
        }
    }

    if (Test-Path $pgHbaFile) {
        $hba = Get-Content $pgHbaFile -Raw
        if ($hba -notmatch "host\s+all\s+all\s+0\.0\.0\.0/0\s+md5") {
            Add-Content -Path $pgHbaFile -Value "`nhost    all    all    0.0.0.0/0    md5"
            Log "pg_hba.conf: added Docker access rule" "OK"
        }
    }

    # Wait for service then restart
    Log "Waiting for PostgreSQL service..."
    for ($i = 1; $i -le 30; $i++) {
        $svc = Get-Service "postgresql-syslog" -ErrorAction SilentlyContinue
        if ($svc -and $svc.Status -eq "Running") {
            Restart-Service "postgresql-syslog" -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 5
            Log "PostgreSQL service running" "OK"
            return
        }
        Start-Sleep -Seconds 2
    }
    Log "PostgreSQL service did not start within 60s" "WARN"
}

# ======================== STEP 3: DATABASE SETUP ========================
function Step-InitDatabase {
    Log "=== Step 3: Database Setup ==="

    $psql = Find-Psql
    if (-not $psql) {
        throw "psql not found. Cannot configure database."
    }
    Log "Using psql: $psql"

    # Connect with retries
    $env:PGPASSWORD = $PGSuperPassword
    $connected = $false
    for ($i = 1; $i -le 15; $i++) {
        try {
            $result = & $psql -h $PGHost -p $PGPort -U postgres -tc "SELECT 1" 2>&1
            if ($result -match "1") {
                Log "PostgreSQL connected" "OK"
                $connected = $true
                break
            }
        } catch {}
        Log "  Connection attempt $i/15 failed, retrying in 5s..." "WARN"
        Start-Sleep -Seconds 5
    }
    if (-not $connected) {
        throw "Cannot connect to PostgreSQL at ${PGHost}:${PGPort}"
    }

    # Ensure Docker can connect (for existing PG installs)
    if ($PGHost -eq "localhost" -or $PGHost -eq "127.0.0.1") {
        try {
        
            $dataDir = (& $psql -h $PGHost -p $PGPort -U postgres -tc "SHOW data_directory;" 2>&1 | Out-String).Trim()
            if ($dataDir -and (Test-Path $dataDir)) {
                $hbaFile = Join-Path $dataDir "pg_hba.conf"
                if (Test-Path $hbaFile) {
                    $hba = Get-Content $hbaFile -Raw
                    if ($hba -notmatch "host\s+all\s+all\s+0\.0\.0\.0/0\s+md5") {
                        Add-Content -Path $hbaFile -Value "`nhost    all    all    0.0.0.0/0    md5"
                        & $psql -h $PGHost -p $PGPort -U postgres -c "SELECT pg_reload_conf();" 2>&1 | Out-Null
                        Log "Updated existing PG for Docker access" "OK"
                    }
                }
                $confFile = Join-Path $dataDir "postgresql.conf"
                if (Test-Path $confFile) {
                    $conf = Get-Content $confFile -Raw
                    if ($conf -match "listen_addresses\s*=\s*'localhost'") {
                        $conf = $conf -replace "listen_addresses\s*=\s*'localhost'", "listen_addresses = '*'"
                        $conf | Set-Content $confFile -Encoding UTF8 -Force
                        # Need service restart for listen_addresses
                        $pgSvc = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1
                        if ($pgSvc) {
                            Restart-Service $pgSvc.Name -Force -ErrorAction SilentlyContinue
                            Start-Sleep -Seconds 5
                            Log "Restarted PG for listen_addresses change" "OK"
                            # Reconnect after restart
                            Start-Sleep -Seconds 3
                        }
                    }
                }
            }
        } catch {
            Log "Could not update existing PG config: $($_.Exception.Message)" "WARN"
        } finally {
        
        }
    }

    # Create user (temporarily allow Continue to handle psql NOTICE output)
    Log "Creating user: $PGUser"
    $sql = "DO `$`$ BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname='$PGUser') THEN CREATE ROLE `"$PGUser`" LOGIN PASSWORD '$PGPassword'; END IF; END `$`$;"

    & $psql -h $PGHost -p $PGPort -U postgres -c $sql 2>&1 | Out-Null


    # Create database
    Log "Creating database: $PGDatabase"

    $exists = & $psql -h $PGHost -p $PGPort -U postgres -tc "SELECT 1 FROM pg_database WHERE datname='$PGDatabase'" 2>&1
    if ($exists -notmatch "1") {
        & $psql -h $PGHost -p $PGPort -U postgres -c "CREATE DATABASE `"$PGDatabase`" OWNER `"$PGUser`";" 2>&1 | Out-Null
        Log "Database created" "OK"
    } else {
        Log "Database already exists" "OK"
    }


    # Run schema (psql outputs NOTICE to stderr for IF NOT EXISTS - must not treat as error)
    $sqlFile = Join-Path $InstallDir "sql\init.sql"
    if (Test-Path $sqlFile) {
        $env:PGPASSWORD = $PGPassword
    
        & $psql -h $PGHost -p $PGPort -U $PGUser -d $PGDatabase -f $sqlFile 2>&1 | Out-Null
    
        Log "Schema initialized" "OK"
    }

    # Grant permissions (psql NOTICE messages must not be treated as errors)
    $env:PGPASSWORD = $PGSuperPassword

    & $psql -h $PGHost -p $PGPort -U postgres -d $PGDatabase -c "GRANT ALL ON ALL TABLES IN SCHEMA public TO `"$PGUser`";" 2>&1 | Out-Null
    & $psql -h $PGHost -p $PGPort -U postgres -d $PGDatabase -c "GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO `"$PGUser`";" 2>&1 | Out-Null

    Log "Permissions granted" "OK"

    $env:PGPASSWORD = ""
}

# ======================== STEP 4: TLS CERTS ========================
function Step-GenerateTLS {
    Log "=== Step 4: TLS Certificates ==="
    # Always generate certs - syslog-ng.conf references them and container will crash without them
    if (-not $GenerateTLS) {
        Log "TLS not explicitly requested, but generating certs so container can start" "OK"
    }

    $certDir = Join-Path $InstallDir "certs"
    New-Item -ItemType Directory -Path $certDir -Force -ErrorAction SilentlyContinue | Out-Null

    try {
        $certScript = Join-Path $InstallDir "scripts\Generate-Certs.ps1"
        & $certScript -OutputDir $certDir
        Log "TLS certificates generated" "OK"
    } catch {
        Log "TLS generation failed: $($_.Exception.Message)" "WARN"
        Log "UDP/TCP syslog will still work. Generate certs manually for TLS." "WARN"
    }
}

# ======================== STEP 5: DOCKER COMPOSE + CONTAINER ========================
function Step-StartContainer {
    Log "=== Step 5: Docker Image & Container ==="

    # Verify Docker is ready
    try {
        $null = & docker info 2>&1
        if ($LASTEXITCODE -ne 0) { Step-StartDocker }
    } catch { Step-StartDocker }

    # Load image
    $tarFile = Join-Path $InstallDir "dependencies\syslog-ng.tar"
    if (-not (Test-Path $tarFile)) {
        throw "syslog-ng.tar not found: $tarFile"
    }
    $sizeMB = [math]::Round((Get-Item $tarFile).Length / 1MB)
    Log "Loading Docker image (${sizeMB} MB)..."
    & docker load -i $tarFile 2>&1 | ForEach-Object { Log "  docker: $_" }
    if ($LASTEXITCODE -ne 0) {
        throw "docker load failed (exit code $LASTEXITCODE)"
    }

    # Verify image
    $img = & docker images syslog-ng:latest --format "{{.Repository}}:{{.Tag}}" 2>&1
    if ($img -notmatch "syslog-ng") {
        Log "Available images:" "ERROR"
        & docker images --format "{{.Repository}}:{{.Tag}}" 2>&1 | ForEach-Object { Log "  $_" "ERROR" }
        throw "Image syslog-ng:latest not found after load."
    }
    Log "Image loaded: $img" "OK"

    # Docker PG host translation
    $dockerPGHost = $PGHost
    if ($PGHost -in @("localhost", "127.0.0.1", "::1")) {
        $dockerPGHost = "host.docker.internal"
        Log "PG host for Docker: $dockerPGHost"
    }

    # Generate docker-compose.yml
    $payloadDir = Join-Path $InstallDir "payload"
    $composeContent = @"
services:
  syslog-ng:
    image: syslog-ng:latest
    container_name: syslog-ng
    restart: unless-stopped
    extra_hosts:
      - "host.docker.internal:host-gateway"
    environment:
      SQL_HOST: "$dockerPGHost"
      SQL_PORT: "$PGPort"
      SQL_USER: "$PGUser"
      SQL_PASS: "$PGPassword"
      SQL_DB: "$PGDatabase"
    ports:
      - "${SyslogPort}:514/udp"
      - "${SyslogPort}:514/tcp"
      - "${TLSPort}:6514/tcp"
    volumes:
      - ../certs:/etc/syslog-ng/certs:ro
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "3"
"@
    $composeContent | Out-File -FilePath (Join-Path $payloadDir "docker-compose.yml") -Encoding UTF8 -Force
    Log "docker-compose.yml generated"

    # Stop existing container (ignore errors if not running)
    & docker stop syslog-ng 2>&1 | Out-Null
    & docker rm syslog-ng 2>&1 | Out-Null

    # Start container
    Push-Location $payloadDir
    try {
        Log "Starting syslog-ng container..."
        & docker compose up -d 2>&1 | ForEach-Object { Log "  docker: $_" }
        if ($LASTEXITCODE -ne 0) {
            $logs = & docker logs syslog-ng 2>&1
            if ($logs) { $logs | ForEach-Object { Log "  log: $_" "ERROR" } }
            throw "docker compose up failed (exit code $LASTEXITCODE)"
        }
    } finally { Pop-Location }

    # Verify
    Start-Sleep -Seconds 5
    $status = & docker ps --filter "name=syslog-ng" --format "{{.Status}}" 2>&1
    if ($status -like "Up*") {
        Log "Container running: $status" "OK"
    } else {
        Log "Container status: $status" "WARN"
        & docker logs syslog-ng --tail 20 2>&1 | ForEach-Object { Log "  $_" "WARN" }
    }
}

# ======================== STEP 6: FIREWALL ========================
function Step-Firewall {
    Log "=== Step 6: Firewall ==="
    try {
        $fwScript = Join-Path $InstallDir "scripts\Configure-Firewall.ps1"
        & $fwScript -SyslogPort ([int]$SyslogPort) -TLSPort ([int]$TLSPort) -PGPort ([int]$PGPort)
        Log "Firewall configured" "OK"
    } catch {
        Log "Firewall config failed: $($_.Exception.Message)" "WARN"
        Log "You may need to open ports manually" "WARN"
    }
}

# ======================== MAIN ========================
try {
    Write-Diagnostics

    Step-InstallDocker
    Step-InstallPostgres
    Step-InitDatabase
    Step-GenerateTLS
    Step-StartContainer
    Step-Firewall

    Log ""
    Log "======================================"
    Log "  INSTALLATION COMPLETE" "OK"
    Log "======================================"
    Log "  Syslog: UDP+TCP port $SyslogPort, TLS port $TLSPort"
    Log "  PostgreSQL: ${PGHost}:${PGPort}/${PGDatabase}"
    Log "  Test: echo '<14>Test message' | ncat -u localhost $SyslogPort"
    Log "======================================"
    exit 0

} catch {
    Log "" "ERROR"
    Log "=====================================" "ERROR"
    Log "  INSTALLATION FAILED" "ERROR"
    Log "  $($_.Exception.Message)" "ERROR"
    Log "  Line: $($_.InvocationInfo.ScriptLineNumber)" "ERROR"
    Log "=====================================" "ERROR"

    Write-Host ""
    Write-Host "Press any key to close..." -ForegroundColor Yellow
    try { $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") } catch {}
    exit 1
}
