# Dependencies - Offline Packages

Place the following offline installer files in this folder before building.

## Required Files

### 1. Docker Desktop (offline installer)
- **Filename:** `DockerDesktop-x64.exe`
- **Target: LTSC 2019 (Build 17763) → You MUST use Docker Desktop 4.15.x**
  - 4.15 is the LAST version that supports Windows 10 Build 17763 via Hyper-V
  - DO NOT use newer versions — they will fail to install on LTSC 2019
- **Download from (on a machine with internet):**
  - Docker Desktop 4.15.0: https://docs.docker.com/desktop/release-notes/#4150
  - Look for the "Docker Desktop Installer.exe" download link on that page
  - Rename downloaded file to: `DockerDesktop-x64.exe`
- **Note:** Hyper-V must be enabled on the target server (installer handles this automatically)

### 2. PostgreSQL 16 (offline installer by EDB)
- **Filename:** `postgresql-16.8-1-windows-x64.exe` (or latest 16.x)
- **Download from:**
  - https://www.enterprisedb.com/downloads/postgres-postgresql-downloads
  - Select: Windows x86-64, Version 16.x
- **Note:** Keep the original filename (starts with `postgresql-`)

### 3. Syslog-NG Docker Image (pre-built tar)
- **Filename:** `syslog-ng.tar`
- **How to create (on a machine with Docker and internet):**

```bash
# Navigate to the payload directory
cd payload

# Build the image
docker build -t syslog-ng:latest .

# Save as tar file
docker save syslog-ng:latest -o ../dependencies/syslog-ng.tar
```

## Optional Files

### OpenSSL for Windows (if TLS cert generation is needed without Docker)
- Only needed if you want to generate certs on a machine without OpenSSL
- The installer can use PowerShell's built-in certificate generation as fallback

## Version Compatibility Matrix

| Windows Version | Docker Desktop Version | Backend |
|----------------|----------------------|---------|
| LTSC 2019 (17763) | 4.15.x (max) | Hyper-V |
| LTSC 2021 (19044) | 4.37.x+ | WSL2 |
| LTSC 2024 (26100) | 4.37.x+ | WSL2 |

## File Size Estimates

| File | Approximate Size |
|------|-----------------|
| DockerDesktop-x64.exe | ~550 MB |
| postgresql-16.x-x64.exe | ~320 MB |
| syslog-ng.tar | ~250 MB |
| **Total** | **~1.1 GB** |

The final installer .exe will be approximately **1.1 - 1.3 GB** due to LZMA compression.
