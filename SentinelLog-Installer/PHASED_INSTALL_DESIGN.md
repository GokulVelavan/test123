# Phased Installation Design (Auto-Resume Pattern)

## Overview
Two-phase installation with automatic resume after restart. Phase 1 enables Windows features (requires restart), Phase 2 installs all services. Transparent to user via automatic RunOnce registry entry.

## Architecture

### Phase 1: Windows Features & Preparation (Pre-Restart)
**Trigger:** User launches Setup.exe

```
1. Show normal wizard
2. Validate prerequisites
3. Show PreFlight summary
4. On "Install" click:
   a. Extract all files to {app}
   b. Save install parameters to registry under:
      HKLM\Software\SyslogStack\Install\
   c. Enable Hyper-V/WSL2 (if needed)
   d. Register RunOnce entry:
      HKLM\Software\Microsoft\Windows\CurrentVersion\RunOnce\SyslogStackResume
      => "C:\Path\Setup.exe /RESUME"
   e. Request restart
   f. Close installer on restart
```

**Registry State (Phase 1 → Phase 2 handoff):**
```
HKLM\Software\SyslogStack\Install\
├── InstallDir        = "C:\SyslogStack"
├── InstallState      = "Phase1_Complete"
├── SyslogPort        = "514"
├── TLSPort           = "6514"
├── PGHost            = "localhost"
├── PGSuperPassword   = "Deliverain@123$"
├── SelectComponents  = "docker;postgres;syslogng;firewall;tls"
├── EnableHyperV      = 1 or 0
├── EnableWSL2        = 1 or 0
└── Timestamp         = "2026-03-13 10:30:00"
```

### Phase 2: Application Setup (Post-Restart)
**Trigger:** Automatic via RunOnce (or manual: `Setup.exe /RESUME`)

```
1. Detect /RESUME parameter
2. Read registry state
3. Skip all wizard pages
4. Show "Installation in Progress" page
5. Wait 30-45 seconds (Docker Desktop stabilization)
6. Run PowerShell Phase 2 script with parameters from registry
   ✓ Install PostgreSQL (if docker component selected)
   ✓ Create database & user
   ✓ Generate TLS certificates
   ✓ Load Docker image
   ✓ Start syslog-ng container
   ✓ Configure firewall rules
7. Show completion page (success/error)
```

## State Lifecycle

| State | Location | Lifetime | Purpose |
|-------|----------|----------|---------|
| `Phase1_Complete` | Registry | Phase 1 → Phase 2 | Detection flag for resume |
| Install parameters | Registry | Phase 1 → Phase 2 | User's configuration choices |
| RunOnce entry | Registry | Auto-deleted after Phase 2 | Auto-launch trigger |

## Error Scenarios & Handling

### Scenario 1: User cancels Phase 1 before restart
- **Action:** Delete registry entry
- **Result:** Next launch starts fresh Phase 1

### Scenario 2: System restart fails/doesn't happen
- **Action:** Manual restart required (user sees prompt)
- **Result:** RunOnce triggers Phase 2

### Scenario 3: Phase 2 fails
- **Action:** Keep registry state, allow retry via `Setup.exe /RESUME`
- **Result:** User can re-run without losing configuration

### Scenario 4: Partial Phase 2 completion
- **Action:** Error log saved to install.log
- **Result:** User can check log and troubleshoot

## File Layout

```
Setup.exe (/RESUME parameter detection)
├── Phase 1 Code (InitializeWizard, CreatePages, NextButtonClick)
│   └── Calls: CurStepChanged → save state & restart
│
└── Phase 2 Code (Resume detection)
    └── Calls: Install-SyslogStack.ps1 with phase=2 parameter

Install-SyslogStack.ps1 (phase-aware)
├── if (phase -eq 1) → Enable features only, exit
└── if (phase -eq 2) → Install all services
```

## Implementation Checklist

- [ ] Setup.iss: Add resume parameter detection
- [ ] Setup.iss: Add registry state persistence functions
- [ ] Setup.iss: Skip wizard pages if /RESUME detected
- [ ] Setup.iss: Show "Installation in Progress" page in Phase 2
- [ ] Setup.iss: Add RunOnce registry entry before restart
- [ ] Install-SyslogStack.ps1: Add -Phase parameter
- [ ] Install-SyslogStack.ps1: Add 30-45 sec wait for Docker stability
- [ ] Install-SyslogStack.ps1: Add rollback logic on error
- [ ] Test Phase 1 → restart → Phase 2 flow
- [ ] Test error scenarios (cancel, partial failure, manual restart)

## Stability & Timeouts

**Docker Desktop Stabilization (after Hyper-V enable):**
- Wait: 30-45 seconds minimum
- Check: Docker daemon responding
- Retry: 5 attempts with 5-sec backoff

**PostgreSQL Readiness:**
- Wait: Up to 30 seconds for service start
- Check: Can connect to localhost:5432
- Retry: 10 attempts with 3-sec backoff

**Network Readiness:**
- Wait: Up to 15 seconds for IP stack
- Check: Can resolve localhost
- Retry: 5 attempts with 3-sec backoff

## User Experience Flow

```
[Phase 1 Wizard Page 1: Welcome]
  ↓ Next
[Phase 1 Wizard Page 2: Syslog Configuration]
  ↓ Next
[Phase 1 Wizard Page 3: TLS Configuration]
  ↓ Next
[Phase 1 Wizard Page 4: Ready to Install]
  ↓ Install

  [Enabling Hyper-V...] (silent)
  [Saving installation state...] (silent)
  [Creating restart trigger...] (silent)
  [Requesting restart...]

USER RESTARTS SYSTEM

  [RunOnce auto-launches Setup.exe /RESUME]
  [Reads registry state]

[Phase 2 Page: Installation in Progress]
  [Installation Step 1: Docker Desktop...]
  [Installation Step 2: PostgreSQL...]
  [Installation Step 3: Database Setup...]
  [Installation Step 4: TLS Certificates...]
  [Installation Step 5: Syslog Container...]
  [Installation Step 6: Firewall Rules...]

[Phase 2 Page: Installation Complete]
  [Success message + log location]
  ↓ Finish

Installer closes, system ready to use
```

## Success Criteria

✅ Installation completes end-to-end without user manual intervention
✅ Auto-resume triggered automatically (no manual Setup.exe command needed)
✅ Wizard pages hidden during Phase 2 (only progress shown)
✅ State persists across restart
✅ Clear error messages if something fails
✅ Professional, clean user experience
