# Syslog Machine Agent

A cross-platform C#/.NET 8 console application that collects system logs from Windows and Linux machines and forwards them to a remote syslog server using RFC 3164 over UDP or TCP.

## Features

- **Cross-platform**: Runs on Windows and Linux with a single codebase
- **Dual-mode collection**:
  - Real-time streaming via OS event APIs and file watchers
  - Periodic batch collection to catch up missed events during downtime
- **Log sources**:
  - **Windows**: Windows Event Logs (Application, System, Security channels)
  - **Linux**: journalctl, /var/log/syslog, /var/log/messages, /var/log/auth.log
  - **Custom**: File-based log watching on any path with pattern matching
- **Deduplication**: Prevents duplicate log entries through offset tracking
- **RFC 3164 compliant**: Formats and sends logs in standard syslog format
- **UDP and TCP support**: Configurable transport protocol
- **Crash recovery**: Persistent state tracking via JSON file

## Prerequisites

- **.NET 8 SDK or Runtime** - Download from [https://dotnet.microsoft.com/en-us/download/dotnet/8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
  - Requires **SDK** to build from source
  - Requires **Runtime** (included in SDK) to run published binaries

### Linux Prerequisites
```bash
# Debian/Ubuntu
sudo apt-get install dotnet-sdk-8.0

# RHEL/CentOS
sudo dnf install dotnet-sdk-8.0

# Fedora
sudo dnf install dotnet-sdk-8.0
```

### Windows Prerequisites
- Windows 7 SP1 or later, Windows Server 2012 or later
- Download and run the [.NET 8 SDK installer](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

## Project Structure

```
d:/Syslog-Machine-agent/
├── SyslogAgent.sln              # Visual Studio solution
├── src/SyslogAgent/
│   ├── SyslogAgent.csproj       # Project file
│   ├── Program.cs               # Entry point & DI setup
│   ├── appsettings.json         # Configuration
│   ├── Configuration/           # Config classes
│   ├── Models/                  # LogEvent, enums
│   ├── Pipeline/                # LogChannel, LogPipeline
│   ├── Collectors/              # Collector implementations
│   │   ├── Windows/             # Windows Event Log collectors
│   │   ├── Linux/               # Journald and syslog file collectors
│   │   └── Common/              # File watcher collectors (cross-platform)
│   ├── Syslog/                  # RFC 3164 formatting and sending
│   └── Deduplication/           # Offset tracking for crash recovery
└── publish/                     # Output directory for builds
```

## Configuration

Edit `appsettings.json` to configure the syslog server and log sources:

```json
{
  "Agent": {
    "SyslogServer": {
      "Host": "192.168.1.100",    // Syslog server address
      "Port": 514,                 // Syslog port (514=UDP, 601=TCP)
      "Protocol": "UDP",           // "UDP" or "TCP"
      "TcpTimeoutSeconds": 10,
      "MaxMessageBytes": 1024
    },
    "Collectors": {
      "WindowsEventLog": true,
      "WindowsEventLogChannels": ["Application", "System", "Security"],
      "LinuxJournald": true,
      "LinuxSyslogFiles": [
        "/var/log/syslog",
        "/var/log/messages",
        "/var/log/auth.log"
      ],
      "FileWatchers": [
        {
          "Path": "/var/log/myapp",
          "Filter": "*.log",
          "Recursive": false,
          "Facility": "Local0",
          "Severity": "Informational"
        }
      ]
    },
    "BatchIntervalSeconds": 60,    // Batch collection interval
    "ChannelCapacity": 10000,      // Internal channel buffer size
    "EnableRealtime": true,        // Enable real-time collectors
    "EnableBatch": true,           // Enable batch collectors
    "DeduplicationStorePath": "offsets.json"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SyslogAgent": "Debug"
    }
  }
}
```

### Configuration Options

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| SyslogServer.Host | string | "127.0.0.1" | Remote syslog server hostname/IP |
| SyslogServer.Port | int | 514 | Syslog server port |
| SyslogServer.Protocol | string | "UDP" | Transport protocol: "UDP" or "TCP" |
| Collectors.WindowsEventLog | bool | true | Enable Windows Event Log collection |
| Collectors.LinuxJournald | bool | true | Enable journald collection |
| BatchIntervalSeconds | int | 60 | Seconds between batch collection runs |
| EnableRealtime | bool | true | Enable real-time log streaming |
| EnableBatch | bool | true | Enable periodic batch sync |

## Building from Source

### Prerequisites
- .NET 8 SDK installed and in PATH

### Build Steps

```bash
# Clone or navigate to the project directory
cd d:/Syslog-Machine-agent

# Restore dependencies
dotnet restore

# Build for debugging
dotnet build

# Or build for release (optimized)
dotnet build -c Release
```

## Running

### Development (Local Testing)

```bash
# Run in debug mode
cd d:/Syslog-Machine-agent
dotnet run --project src/SyslogAgent/SyslogAgent.csproj

# Or from the src/SyslogAgent directory
dotnet run
```

### Production (Self-Contained Single-File Executable)

#### Windows (x64)
```bash
dotnet publish src/SyslogAgent/SyslogAgent.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o ./publish/win-x64
```

Binary location: `./publish/win-x64/SyslogAgent.exe`

#### Linux (x64)
```bash
dotnet publish src/SyslogAgent/SyslogAgent.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o ./publish/linux-x64
```

Binary location: `./publish/linux-x64/SyslogAgent`

#### Linux (ARM64 - Raspberry Pi, Cloud VMs)
```bash
dotnet publish src/SyslogAgent/SyslogAgent.csproj \
  -c Release \
  -r linux-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false \
  -o ./publish/linux-arm64
```

### Running the Published Binary

**Windows:**
```bash
./publish/win-x64/SyslogAgent.exe
```

**Linux:**
```bash
chmod +x ./publish/linux-x64/SyslogAgent
./publish/linux-x64/SyslogAgent
```

### Running as a Service

#### Windows (Task Scheduler or Service)
Create a scheduled task or Windows Service pointing to the executable. Example with NSSM (Non-Sucking Service Manager):
```powershell
nssm install SyslogAgent "C:\path\to\SyslogAgent.exe"
nssm start SyslogAgent
```

#### Linux (systemd)
Create `/etc/systemd/system/syslog-agent.service`:
```ini
[Unit]
Description=Syslog Machine Agent
After=network.target

[Service]
Type=simple
ExecStart=/opt/syslog-agent/SyslogAgent
WorkingDirectory=/opt/syslog-agent
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
```

Enable and start:
```bash
sudo systemctl enable syslog-agent
sudo systemctl start syslog-agent
sudo systemctl status syslog-agent
```

## Testing

### Test Setup: Local Syslog Server

#### Linux (with netcat)
```bash
# Listen on UDP port 514 and log to a file
nc -ulvp 514 > syslog.log &

# Or use rsyslog/syslog-ng for a real syslog server
# Edit /etc/rsyslog.conf to enable UDP listening:
# $ModLoad imudp
# $UDPServerRun 514
sudo systemctl restart rsyslog
```

#### Windows (with PowerShell)
Create a simple UDP listener script:
```powershell
$udpListener = New-Object System.Net.Sockets.UdpClient(514)
while ($true) {
    $remoteEndpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 514)
    $bytes = $udpListener.Receive([ref]$remoteEndpoint)
    $message = [System.Text.Encoding]::UTF8.GetString($bytes)
    Write-Host "[$((Get-Date).ToString())] $message"
}
```

### Generate Test Logs

#### Windows
```powershell
# Create a test Application log event
eventcreate /T INFORMATION /ID 1000 /L APPLICATION /D "Test message from syslog agent"
```

#### Linux
```bash
# Log a message to syslog
logger "Test message from syslog agent"

# Or trigger from a running application
echo "Test log line" >> /var/log/syslog
```

### Verify

1. Start a local syslog listener (see above)
2. Edit `appsettings.json` to point to `127.0.0.1:514`
3. Run the agent: `dotnet run`
4. Generate test logs
5. Verify RFC 3164 formatted messages appear in the listener

Expected format:
```
<134>Mar 12 14:30:45 HOSTNAME app-name: Test message from syslog agent
```

## Output Files

- `offsets.json` - Persisted state tracking (byte offsets and cursors)
  - Location: Same directory as the running executable
  - Format: JSON dictionary mapping file paths/channel names to offsets
  - Auto-created on first run
  - Enables crash recovery

## Troubleshooting

### "No log collectors were configured"
- Check that `appsettings.json` has at least one collector enabled
- Verify the Windows Event Log channels exist (Application, System, Security)
- On Linux, check that `/var/log` files exist

### "Failed to connect to syslog server"
- Verify the syslog server is running and listening
- Check the Host and Port in `appsettings.json`
- Ensure network connectivity (firewall, routing)
- For TCP, increase `TcpTimeoutSeconds` if the network is slow

### Windows Event Log collector not working
- Verify you're running with sufficient privileges (Administrator)
- Check that the Event Log channels exist: `Get-EventLog -List`

### Journald collector not working on Linux
- Verify `journalctl` is available: `which journalctl`
- Check permissions: `sudo journalctl` should work without error
- On some systems, users need to be in the `systemd-journal` group:
  ```bash
  sudo usermod -a -G systemd-journal $USER
  ```

### High CPU usage
- Reduce `ChannelCapacity` if log volume is high
- Increase `BatchIntervalSeconds` to reduce batch frequency
- Check if a log source is generating extremely high volume

### Memory issues
- Lower `ChannelCapacity` (default 10,000)
- Disable unnecessary collectors in `appsettings.json`
- Monitor `/var/log` file sizes on Linux

## Architecture

```
┌─────────────────────────────────────┐
│     Windows Event Logs               │
│     Linux Journald                   │
│     File System (/var/log/*)         │
└────────────────────┬────────────────┘
                     │
        ┌────────────┴────────────┐
        │                         │
   ┌────▼──────┐         ┌───────▼──┐
   │ Real-time │         │  Batch   │
   │Collectors │         │Collectors│
   └────┬──────┘         └───────┬──┘
        │    (advances offsets)  │
        └────────────┬───────────┘
                     │
              ┌──────▼──────┐
              │  LogChannel │  (bounded 10k)
              │  (thread-   │
              │   safe)     │
              └──────┬──────┘
                     │
         ┌───────────▼──────────┐
         │    LogPipeline       │
         │  (BackgroundService) │
         └───────────┬──────────┘
                     │
         ┌───────────▼──────────┐
         │   SyslogSender       │  RFC 3164 format
         │ (UDP or TCP)         │
         └───────────┬──────────┘
                     │
         ┌───────────▼──────────┐
         │  Remote Syslog       │
         │  Server (514)        │
         └──────────────────────┘
```

## Key Components

### Collectors
- **ILogCollector**: Base interface
- **IRealtimeCollector**: Streams events as they arrive (OS watchers)
- **IBatchCollector**: Periodically reads from offsets (catch-up)
- **CollectorFactory**: Creates platform-specific collectors

### Pipeline
- **LogChannel**: Thread-safe `System.Threading.Channels.Channel<LogEvent>`
- **LogPipeline**: BackgroundService reading from channel, writing to syslog sender
- **LogEvent**: Central DTO carrying all log metadata

### Deduplication
- **IDeduplicationStore**: Interface for offset tracking
- **FileOffsetStore**: JSON file persistence, SemaphoreSlim-synchronized

### Syslog
- **SyslogFormatter**: RFC 3164 byte serialization
- **SyslogSender**: UDP/TCP transport with reconnection logic

## License

[MIT License](LICENSE)

## Support

For issues or feature requests, please refer to the implementation plan in `C:\Users\Hp\.claude\plans\melodic-cuddling-scott.md` or check the inline code documentation.
