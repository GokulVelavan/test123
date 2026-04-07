# Syslog Machine Agent — Cleanup & Refactor Design
**Date:** 2026-04-06  
**Scope:** Full cleanup, bug fixes, code deduplication, all Linux log sources verified, linux-x64 self-contained publish

---

## Goals

1. Remove ASP.NET Core — replace with plain `IHost` (no open ports, smaller binary)
2. Deduplicate shared parsing logic across collectors
3. Remove stale publish folders from repo root
4. Verify and fix all Linux log collectors (journald, syslog files, auto-discovery, binary logs)
5. Ensure all log sources are enabled and wired correctly by default
6. Publish as `linux-x64` self-contained single file (no .NET required on Ubuntu 22)
7. Windows build remains intact and compilable

---

## Section 1 — Remove ASP.NET Core

### What to remove
- `WebApplication.CreateBuilder` → replace with `Host.CreateDefaultBuilder` in `Program.cs`
- `src/SyslogAgent/Api/` folder — delete entirely:
  - `AgentMetrics.cs`
  - `RecentLogBuffer.cs`
  - `LogEventDto.cs`
  - `AgentStatusDto.cs` (if present)
- CORS registration, Kestrel config, `MapApiEndpoints` static method
- `ApiPort` and `EnableApi` fields from `AgentOptions.cs`
- `"ApiPort": 5100` and `"EnableApi": true` from `appsettings.json`
- `AgentMetrics` and `RecentLogBuffer` parameters from `LogPipeline` constructor

### What stays
- Interactive startup prompt (IP, port, protocol)
- Console status output on startup
- All collectors, pipeline, deduplication — unchanged

### Result
- Pure console daemon via `IHost` + `BackgroundService`
- No open TCP ports
- Binary ~15MB smaller
- No ASP.NET Core dependency

---

## Section 2 — Code Deduplication

### Problem
Two pairs of classes contain identical private methods:

| Duplicated Method | In Classes |
|---|---|
| `ParseJournaldJson(string, CollectionMode)` | `JournaldCollector` + `JournaldBatchCollector` |
| `MapToLogEvent(EventRecord, CollectionMode)` | `WindowsEventLogCollector` + `WindowsEventLogBatchCollector` |

### Fix
Extract each into a dedicated static helper class:

**`Collectors/Linux/JournaldParser.cs`**
- `static LogEvent? Parse(string json, CollectionMode mode)`
- `static string ExtractCursor(string json)`
- `static bool IsValidJournaldMatch(string match)`
- `static bool IsValidJournaldCursor(string cursor)`

**`Collectors/Windows/WindowsEventLogMapper.cs`**
- `static LogEvent Map(EventRecord record, CollectionMode mode)`

Both collector classes call the shared helper. No behavior change — pure deduplication.

---

## Section 3 — Remove Stale Publish Folders

Delete from repo root:
- `publish/`
- `publish-linux/`
- `publish-new/`
- `publish2/`

These are build output artifacts — they belong in `.gitignore`, not the repo.

---

## Section 4 — Linux Collector Verification & Fixes

### Collectors to verify
| Collector | Mode | Source |
|---|---|---|
| `JournaldCollector` | Realtime | `journalctl -f --output=json` |
| `JournaldBatchCollector` | Batch | `journalctl --output=json --after-cursor` |
| `SyslogFileCollector` | Realtime | FileSystemWatcher on `/var/log/*` |
| `SyslogFileBatchCollector` | Batch | File read from last byte offset |
| `LinuxLogDiscoveryCollector` | Both | Auto-discover all `/var/log/**` text files |
| `LinuxBinaryLogCollector` | Batch | `last -F`, `lastb -F`, `lastlog` |
| `DmesgBatchCollector` | Batch | `dmesg` kernel ring buffer |

### Known issues to fix
1. `DmesgBatchCollector` — review for correctness; currently disabled by default (`LinuxDmesg: false`) — keep disabled (journald covers kernel messages on Ubuntu 22 which has journald)
2. `JournaldBatchCollector.RunJournalctlAsync` — no timeout on process wait; add 30s timeout with `CancellationTokenSource`
3. `LinuxLogDiscoveryCollector.ReadFileAsync` — emits `CollectionMode.Realtime` even during batch scan; fix to pass correct mode
4. `SyslogFileBatchCollector` — verify it skips files that don't exist gracefully (Ubuntu 22 may not have all paths)

### Default config verification
Ensure `appsettings.json` defaults:
- `LinuxJournald: true`
- `LinuxBinaryLogs: true`
- `LinuxAutoDiscoverLogs: true`
- `EnableRealtime: true`
- `EnableBatch: true`
- `ApplyFilter: false` (collect ALL logs, not filtered)

---

## Section 5 — Publish

### Linux (primary target)
```bash
dotnet publish src/SyslogAgent -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ./out/linux-x64
```
Output: single binary `SyslogAgent` in `out/linux-x64/` + `appsettings.json`

### Windows (kept working, not published)
```bash
dotnet build src/SyslogAgent -c Release -r win-x64
```
Must compile cleanly with zero errors.

### Self-contained means
- .NET 8 runtime bundled inside binary
- Ubuntu 22 requires only: `libicu70`, `libssl3`, `libc6` (all pre-installed)
- No `dotnet` CLI needed on the target machine

---

## File Changes Summary

| Action | Target |
|---|---|
| Modify | `Program.cs` — remove WebApplication, use IHost |
| Modify | `Pipeline/LogPipeline.cs` — remove AgentMetrics/RecentLogBuffer |
| Modify | `Configuration/AgentOptions.cs` — remove ApiPort, EnableApi |
| Modify | `appsettings.json` — remove ApiPort, EnableApi entries |
| Delete | `Api/` folder (4 files) |
| Delete | `publish/`, `publish-linux/`, `publish-new/`, `publish2/` folders |
| Create | `Collectors/Linux/JournaldParser.cs` |
| Create | `Collectors/Windows/WindowsEventLogMapper.cs` |
| Modify | `Collectors/Linux/JournaldCollector.cs` — use JournaldParser |
| Modify | `Collectors/Linux/JournaldBatchCollector.cs` — use JournaldParser, add timeout |
| Modify | `Collectors/Windows/WindowsEventLogCollector.cs` — use WindowsEventLogMapper |
| Modify | `Collectors/Windows/WindowsEventLogBatchCollector.cs` — use WindowsEventLogMapper |
| Modify | `Collectors/Linux/LinuxLogDiscoveryCollector.cs` — fix CollectionMode in batch |
| Modify | `SyslogAgent.csproj` — change SDK from `Microsoft.NET.Sdk.Web` to `Microsoft.NET.Sdk` |

---

## Section 6 — Unit Tests

Add a new test project `src/SyslogAgent.Tests/` targeting `net8.0` with `xunit`.

### Tests to cover
| Test Class | What it tests |
|---|---|
| `JournaldParserTests` | Valid JSON parses correctly, missing MESSAGE returns null, bad JSON returns null, timestamp microseconds convert correctly, priority maps to SyslogSeverity |
| `WindowsEventLogMapperTests` | Level → SyslogSeverity mapping, LogName → SyslogFacility mapping |
| `SyslogFormatterTests` | RFC 3164 format output, truncation at maxBytes, UTF-8 boundary truncation |

These are pure unit tests — no I/O, no process spawning, no network. Fast and reliable.

---

## Non-Goals
- No Avalonia UI (separate project)
- No new log sources beyond what already exists
- No changes to Windows collectors behavior
