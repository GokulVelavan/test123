#!/usr/bin/env bash
# =============================================================================
# SentinelLog – Linux Package Builder
# Run this ONCE on an internet-connected machine to produce 3 offline bundles.
#
# Usage:  bash package.sh
#
# Output (in ./dist/):
#   sentinellog-agent-linux.tar.gz
#   sentinellog-receiver-linux.tar.gz
#   sentinellog-worker-linux.tar.gz
#
# Requirements on THIS machine (the packaging machine):
#   - Ubuntu/Debian (apt-get available)
#   - dotnet SDK 8 installed  (to publish the .NET projects)
#   - Internet access
# =============================================================================
set -e

# ── Paths — adjust if your repo is in a different location ────────────────────
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

AGENT_DIR="$REPO_ROOT/SentinelLog-Agent-Linux"
RECEIVER_DIR="$REPO_ROOT/SentinelLog-Receiver-Linux"
WORKER_DIR="$REPO_ROOT/SentinelLog-Portal/drs.scaffold.syslog"

DIST_DIR="$SCRIPT_DIR/dist"
WORK_DIR="$SCRIPT_DIR/.build"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
info()    { echo -e "${GREEN}==> $*${NC}"; }
warn()    { echo -e "${YELLOW}    WARNING: $*${NC}"; }
error()   { echo -e "${RED}    ERROR: $*${NC}"; exit 1; }
section() { echo ""; echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"; \
            echo -e "${CYAN}  $*${NC}"; \
            echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"; echo ""; }

# =============================================================================
# PRE-FLIGHT CHECKS
# =============================================================================
section "Pre-flight checks"

command -v apt-get &>/dev/null || error "This script requires apt-get (Ubuntu/Debian packaging machine)."
command -v dotnet  &>/dev/null || error ".NET SDK not found. Install dotnet-sdk-8.0 first."

DOTNET_VER=$(dotnet --version 2>/dev/null | cut -d. -f1)
[ "$DOTNET_VER" -ge 8 ] 2>/dev/null || error ".NET SDK 8+ required (found: $(dotnet --version))."

echo "  dotnet SDK : $(dotnet --version)"
echo "  repo root  : $REPO_ROOT"
echo "  output dir : $DIST_DIR"

mkdir -p "$DIST_DIR"
rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR"

# =============================================================================
# STEP 1 — Download offline .deb packages
# =============================================================================
section "Step 1 — Downloading offline packages"

DEB_DOTNET="$WORK_DIR/dotnet-runtime"
DEB_SYSLOG="$WORK_DIR/syslog-packages"
mkdir -p "$DEB_DOTNET" "$DEB_SYSLOG"

info "Downloading .NET 8 runtime debs..."
# Add Microsoft feed if not already present
if ! apt-cache show dotnet-runtime-8.0 &>/dev/null; then
    warn ".NET 8 not in apt cache — adding Microsoft package feed..."
    UBUNTU_VER=$(lsb_release -rs 2>/dev/null || echo "22.04")
    UBUNTU_CODENAME=$(lsb_release -cs 2>/dev/null || echo "jammy")
    wget -q "https://packages.microsoft.com/config/ubuntu/$UBUNTU_VER/packages-microsoft-prod.deb" \
        -O /tmp/packages-microsoft-prod.deb
    dpkg -i /tmp/packages-microsoft-prod.deb >/dev/null 2>&1
    apt-get update -qq
fi

# Download (not install) dotnet runtime + deps
apt-get install -y --download-only dotnet-runtime-8.0 >/dev/null 2>&1 || true
# Copy downloaded debs to our bundle folder
find /var/cache/apt/archives/ -name "dotnet-runtime*8*" -o \
                               -name "dotnet-host*" -o \
                               -name "dotnet-hostfxr*8*" -o \
                               -name "aspnetcore-runtime*8*" 2>/dev/null \
    | xargs -I{} cp {} "$DEB_DOTNET/" 2>/dev/null || true

# If none found in cache, download directly
if [ -z "$(ls "$DEB_DOTNET"/*.deb 2>/dev/null)" ]; then
    warn "apt cache empty — downloading directly..."
    (cd "$DEB_DOTNET" && apt-get download \
        dotnet-runtime-8.0 \
        dotnet-host \
        dotnet-hostfxr-8.0 \
        aspnetcore-runtime-8.0 \
        libicu70 libicu72 libicu74 libssl3 2>/dev/null || true)
fi

DEB_COUNT=$(ls "$DEB_DOTNET"/*.deb 2>/dev/null | wc -l)
echo "     Downloaded $DEB_COUNT .deb file(s) for .NET 8 runtime."
[ "$DEB_COUNT" -eq 0 ] && warn "No .NET debs downloaded — factory machines will need internet or manual install."

info "Downloading syslog-ng + python3 packages..."
(cd "$DEB_SYSLOG" && apt-get download \
    syslog-ng \
    syslog-ng-core \
    syslog-ng-scl \
    python3 \
    python3-psycopg2 \
    openssl \
    libpq5 2>/dev/null || true)

SYSLOG_COUNT=$(ls "$DEB_SYSLOG"/*.deb 2>/dev/null | wc -l)
echo "     Downloaded $SYSLOG_COUNT .deb file(s) for syslog-ng + python3."

# =============================================================================
# STEP 2 — Build .NET projects
# =============================================================================
section "Step 2 — Building .NET projects"

# ── Agent ──────────────────────────────────────────────────────────────────────
info "Publishing SentinelLog Agent (linux-x64)..."
AGENT_SRC="$AGENT_DIR/src/SyslogAgent/SyslogAgent.csproj"
AGENT_OUT="$AGENT_DIR/publish/SyslogAgent"

if [ -f "$AGENT_SRC" ]; then
    dotnet publish "$AGENT_SRC" \
        -c Release \
        -r linux-x64 \
        --self-contained false \
        -o "$AGENT_OUT" \
        /p:PublishSingleFile=false \
        --nologo -v quiet
    echo "     Agent published to: $AGENT_OUT"
else
    warn "Agent .csproj not found — skipping build (using existing publish/ if present)."
fi

# ── Worker ─────────────────────────────────────────────────────────────────────
info "Publishing SentinelLog Worker (linux-x64)..."
WORKER_SRC="$WORKER_DIR/DRS.Scaffold.Syslog.Worker/DRS.Scaffold.Syslog.Worker.csproj"
WORKER_OUT="$WORKER_DIR/publish-worker-linux"

if [ -f "$WORKER_SRC" ]; then
    dotnet publish "$WORKER_SRC" \
        -c Release \
        -r linux-x64 \
        --self-contained false \
        -o "$WORKER_OUT" \
        /p:PublishSingleFile=false \
        --nologo -v quiet
    echo "     Worker published to: $WORKER_OUT"
else
    warn "Worker .csproj not found — skipping build (using existing publish-worker-linux/ if present)."
fi

# =============================================================================
# STEP 3 — Bundle Agent package
# =============================================================================
section "Step 3 — Bundling sentinellog-agent-linux.tar.gz"

AGENT_BUNDLE="$WORK_DIR/sentinellog-agent"
mkdir -p "$AGENT_BUNDLE"

# Install script
cp "$AGENT_DIR/install.sh" "$AGENT_BUNDLE/"
chmod +x "$AGENT_BUNDLE/install.sh"

# Published binary
if [ -d "$AGENT_OUT" ] && [ -f "$AGENT_OUT/SyslogAgent" ]; then
    mkdir -p "$AGENT_BUNDLE/publish/SyslogAgent"
    cp -r "$AGENT_OUT"/. "$AGENT_BUNDLE/publish/SyslogAgent/"
    echo "     Agent binary included."
else
    warn "Agent binary not found at $AGENT_OUT — bundle will be incomplete."
fi

# .NET runtime debs
if [ -n "$(ls "$DEB_DOTNET"/*.deb 2>/dev/null)" ]; then
    mkdir -p "$AGENT_BUNDLE/dotnet-runtime"
    cp "$DEB_DOTNET"/*.deb "$AGENT_BUNDLE/dotnet-runtime/"
    echo "     .NET runtime debs included."
fi

# README
cat > "$AGENT_BUNDLE/README.txt" <<'TXT'
SentinelLog Agent – Linux Package
==================================
Install:   sudo bash install.sh
Manage:    sentinellog-ctl status
           sentinellog-ctl start
           sentinellog-ctl stop
           sentinellog-ctl logs

Requirements: Ubuntu 20.04/22.04/24.04 or compatible Debian-based distro
TXT

# Pack
tar -czf "$DIST_DIR/sentinellog-agent-linux.tar.gz" \
    -C "$WORK_DIR" sentinellog-agent

AGENT_SIZE=$(du -sh "$DIST_DIR/sentinellog-agent-linux.tar.gz" | cut -f1)
echo "     Created: sentinellog-agent-linux.tar.gz  ($AGENT_SIZE)"

# =============================================================================
# STEP 4 — Bundle Receiver package
# =============================================================================
section "Step 4 — Bundling sentinellog-receiver-linux.tar.gz"

RECEIVER_BUNDLE="$WORK_DIR/sentinellog-receiver"
mkdir -p "$RECEIVER_BUNDLE"

# Install script
cp "$RECEIVER_DIR/setup.sh" "$RECEIVER_BUNDLE/install.sh"
chmod +x "$RECEIVER_BUNDLE/install.sh"

# init-db.sql (run on postgres server before install)
[ -f "$RECEIVER_DIR/init-db.sql" ] && cp "$RECEIVER_DIR/init-db.sql" "$RECEIVER_BUNDLE/"

# Offline syslog-ng + python packages
if [ -n "$(ls "$DEB_SYSLOG"/*.deb 2>/dev/null)" ]; then
    mkdir -p "$RECEIVER_BUNDLE/offline-packages"
    cp "$DEB_SYSLOG"/*.deb "$RECEIVER_BUNDLE/offline-packages/"
    echo "     Syslog-NG + python3 debs included."
elif [ -d "$RECEIVER_DIR/offline-packages" ] && ls "$RECEIVER_DIR/offline-packages"/*.deb &>/dev/null; then
    cp -r "$RECEIVER_DIR/offline-packages" "$RECEIVER_BUNDLE/"
    echo "     Existing offline-packages/ included."
else
    warn "No syslog-ng debs found — offline-packages/ will be empty."
fi

# README
cat > "$RECEIVER_BUNDLE/README.txt" <<'TXT'
SentinelLog Receiver – Linux Package
======================================
IMPORTANT: Run init-db.sql on your PostgreSQL server FIRST.

Install:   sudo bash install.sh
Manage:    sentinellog-ctl status
           sentinellog-ctl start
           sentinellog-ctl stop  syslog
           sentinellog-ctl stop  postgres
           sentinellog-ctl logs

Ports opened by installer:
  514/udp  1514/udp  601/tcp  1515/tcp  6514/tcp (TLS)
TXT

# Pack
tar -czf "$DIST_DIR/sentinellog-receiver-linux.tar.gz" \
    -C "$WORK_DIR" sentinellog-receiver

RECEIVER_SIZE=$(du -sh "$DIST_DIR/sentinellog-receiver-linux.tar.gz" | cut -f1)
echo "     Created: sentinellog-receiver-linux.tar.gz  ($RECEIVER_SIZE)"

# =============================================================================
# STEP 5 — Bundle Worker package
# =============================================================================
section "Step 5 — Bundling sentinellog-worker-linux.tar.gz"

WORKER_BUNDLE="$WORK_DIR/sentinellog-worker"
mkdir -p "$WORKER_BUNDLE"

# Install script
cp "$WORKER_DIR/install-worker.sh" "$WORKER_BUNDLE/install.sh"
chmod +x "$WORKER_BUNDLE/install.sh"

# Published binary
if [ -d "$WORKER_OUT" ] && [ -f "$WORKER_OUT/DRS.Scaffold.Syslog.Worker" ]; then
    mkdir -p "$WORKER_BUNDLE/publish-worker-linux"
    cp -r "$WORKER_OUT"/. "$WORKER_BUNDLE/publish-worker-linux/"
    echo "     Worker binary included."
else
    warn "Worker binary not found at $WORKER_OUT — bundle will be incomplete."
fi

# .NET runtime debs
if [ -n "$(ls "$DEB_DOTNET"/*.deb 2>/dev/null)" ]; then
    mkdir -p "$WORKER_BUNDLE/dotnet-runtime"
    cp "$DEB_DOTNET"/*.deb "$WORKER_BUNDLE/dotnet-runtime/"
    echo "     .NET runtime debs included."
fi

# README
cat > "$WORKER_BUNDLE/README.txt" <<'TXT'
SentinelLog Worker – Linux Package
=====================================
Install:   sudo bash install.sh
Manage:    sentinellog-ctl status
           sentinellog-ctl start
           sentinellog-ctl stop
           sentinellog-ctl logs worker

Requirements:
  - PostgreSQL must be running and accessible
  - SentinelLog Receiver should be installed and running first
TXT

# Pack
tar -czf "$DIST_DIR/sentinellog-worker-linux.tar.gz" \
    -C "$WORK_DIR" sentinellog-worker

WORKER_SIZE=$(du -sh "$DIST_DIR/sentinellog-worker-linux.tar.gz" | cut -f1)
echo "     Created: sentinellog-worker-linux.tar.gz  ($WORKER_SIZE)"

# =============================================================================
# CLEANUP + SUMMARY
# =============================================================================
rm -rf "$WORK_DIR"

echo ""
echo -e "${CYAN}"
echo "  ╔══════════════════════════════════════════════════════════╗"
echo "  ║   PACKAGES READY                                         ║"
echo "  ╠══════════════════════════════════════════════════════════╣"
printf "  ║  %-56s║\n" "sentinellog-agent-linux.tar.gz     ($AGENT_SIZE)"
printf "  ║  %-56s║\n" "sentinellog-receiver-linux.tar.gz  ($RECEIVER_SIZE)"
printf "  ║  %-56s║\n" "sentinellog-worker-linux.tar.gz    ($WORKER_SIZE)"
echo "  ╠══════════════════════════════════════════════════════════╣"
echo "  ║  Output folder: $DIST_DIR"
echo "  ╠══════════════════════════════════════════════════════════╣"
echo "  ║  Next steps:                                             ║"
echo "  ║   1. Copy needed .tar.gz files to USB                   ║"
echo "  ║   2. On factory machine: tar -xzf <package>.tar.gz      ║"
echo "  ║   3. sudo bash install.sh                                ║"
echo "  ╚══════════════════════════════════════════════════════════╝"
echo -e "${NC}"
