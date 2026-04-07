# Quick Start Guide

Follow these steps to build and run the Syslog Machine Agent.

## Step 1: Install .NET 8 SDK

### On Windows
1. Download from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0
2. Select ".NET 8.0 SDK" (not just Runtime)
3. Run the installer and follow the prompts
4. Verify installation:
   ```powershell
   dotnet --version
   ```
   Should output: `8.0.x`

### On Linux

**Ubuntu/Debian:**
```bash
sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0
```

**RHEL/CentOS/Fedora:**
```bash
sudo dnf install dotnet-sdk-8.0
```

**Verify:**
```bash
dotnet --version
```

## Step 2: Configure the Application

Edit `src/SyslogAgent/appsettings.json`:

```json
{
  "Agent": {
    "SyslogServer": {
      "Host": "YOUR_SYSLOG_SERVER_IP",   // ← Change this
      "Port": 514,                        // ← Change if needed
      "Protocol": "UDP"                   // UDP or TCP
    },
    // ... rest of config
  }
}
```

**Quick test configuration** (local syslog listener):
```json
{
  "SyslogServer": {
    "Host": "127.0.0.1",
    "Port": 514,
    "Protocol": "UDP"
  }
}
```

## Step 3: Build the Project

### From the project root directory:

```bash
# Navigate to project directory
cd d:\Syslog-Machine-agent

# Restore NuGet dependencies
dotnet restore

# Build the project
dotnet build -c Release
```

### Expected output:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Step 4: Run Locally (Testing)

### Start a local syslog listener:

**On Linux:**
```bash
# Terminal 1: Start listener
nc -ulvp 514 > syslog_output.log

# Terminal 2: Run the agent
cd d:/Syslog-Machine-agent
dotnet run --project src/SyslogAgent/SyslogAgent.csproj
```

**On Windows (PowerShell):**
```powershell
# Terminal 1: Start listener (run as Administrator)
$udp = New-Object System.Net.Sockets.UdpClient(514)
while ($true) {
    $remote = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
    $data = $udp.Receive([ref]$remote)
    Write-Host "$(Get-Date): $([System.Text.Encoding]::UTF8.GetString($data))"
}

# Terminal 2: Run the agent
cd d:\Syslog-Machine-agent
dotnet run --project src/SyslogAgent/SyslogAgent.csproj
```

### Generate test logs:

**Windows (in a 3rd terminal):**
```powershell
eventcreate /T INFORMATION /ID 1000 /L APPLICATION /D "Test message from syslog agent"
```

**Linux (in a 3rd terminal):**
```bash
logger "Test message from syslog agent"
```

### Verify:
Look for messages in:
- **Terminal 1** (listener): Should see `<134>Mar 12 ...` formatted syslog messages
- **Terminal 2** (agent): Should see `[Information] SyslogAgent.Pipeline.LogPipeline: LogPipeline started`

## Step 5: Publish for Production

### Windows Executable:
```bash
cd d:\Syslog-Machine-agent

dotnet publish src/SyslogAgent/SyslogAgent.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o ./publish/win-x64

# Output: ./publish/win-x64/SyslogAgent.exe
```

### Linux Executable:
```bash
cd d:/Syslog-Machine-agent

dotnet publish src/SyslogAgent/SyslogAgent.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o ./publish/linux-x64

# Output: ./publish/linux-x64/SyslogAgent
chmod +x ./publish/linux-x64/SyslogAgent
```

### Run Published Binary:

**Windows:**
```powershell
# Copy SyslogAgent.exe and appsettings.json to deployment directory
cd C:\path\to\deployment
.\SyslogAgent.exe
```

**Linux:**
```bash
# Copy SyslogAgent and appsettings.json to deployment directory
cd /opt/syslog-agent
./SyslogAgent
```

## Step 6: Deploy as a Service (Optional)

### Windows Service with NSSM:

```powershell
# Download NSSM from https://nssm.cc/download
# Or install via Chocolatey: choco install nssm

nssm install SyslogAgent "C:\path\to\SyslogAgent.exe"
nssm set SyslogAgent AppDirectory "C:\path\to"
nssm start SyslogAgent

# View status
nssm status SyslogAgent

# Stop
nssm stop SyslogAgent
```

### Linux systemd Service:

Create `/etc/systemd/system/syslog-agent.service`:
```ini
[Unit]
Description=Syslog Machine Agent
After=network.target
Wants=network-online.target

[Service]
Type=simple
ExecStart=/opt/syslog-agent/SyslogAgent
WorkingDirectory=/opt/syslog-agent
Restart=on-failure
RestartSec=10
User=syslog
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl daemon-reload
sudo systemctl enable syslog-agent
sudo systemctl start syslog-agent
sudo systemctl status syslog-agent

# View logs
sudo journalctl -u syslog-agent -f
```

## Common Issues

### .NET SDK not found
```
'dotnet' is not recognized as an internal or external command
```
**Solution**: Ensure .NET 8 SDK is installed and in your PATH. Restart your terminal after installing.

### Build fails with "error NU1101"
```
Unable to resolve package
```
**Solution**:
```bash
dotnet clean
dotnet restore
dotnet build
```

### Permission denied on Linux
```
./SyslogAgent: Permission denied
```
**Solution**:
```bash
chmod +x ./SyslogAgent
./SyslogAgent
```

### Cannot bind to syslog port 514
```
System.Net.Sockets.SocketException: Permission denied
```
**Solution**: Port 514 requires root on Linux:
```bash
sudo ./SyslogAgent

# Or change to port 5514 in appsettings.json and use a different port
```

### Event Log access denied (Windows)
**Solution**: Run PowerShell/Command Prompt as Administrator

### Journald permission denied (Linux)
**Solution**: Add user to systemd-journal group:
```bash
sudo usermod -a -G systemd-journal $USER
newgrp systemd-journal
```

## Next Steps

1. Review `appsettings.json` to customize log sources and syslog server
2. Adjust `BatchIntervalSeconds` (default 60s) based on your needs
3. Enable/disable collectors for your platform (Windows Event Log, Journald, file watchers)
4. Monitor `offsets.json` - this file tracks progress for crash recovery
5. Check logs in the destination syslog server

## Files to Know

- **src/SyslogAgent/appsettings.json** - Main configuration
- **offsets.json** - Crash recovery state (auto-created, don't edit)
- **SyslogAgent.exe** / **SyslogAgent** - The published executable

## More Information

- See **README.md** for architecture and detailed documentation
- Implementation plan: `C:\Users\Hp\.claude\plans\melodic-cuddling-scott.md`
