#!/usr/bin/env bash
# =============================================================================
# SentinelLog Agent – Linux Installer
# Usage:  sudo ./install.sh
# Bundle: publish/SyslogAgent binary + dotnet-runtime/*.deb|rpm (offline)
# =============================================================================
set -e

INSTALL_DIR="/opt/sentinellog-agent"
SERVICE_NAME="sentinellog-agent"
SERVICE_USER="sentinellog"
BINARY_NAME="SyslogAgent"
CTL_BIN="/usr/local/bin/sentinellog-ctl"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PUBLISH_DIR="$SCRIPT_DIR/publish/SyslogAgent"
RUNTIME_DIR="$SCRIPT_DIR/dotnet-runtime"

RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'; NC='\033[0m'
info()  { echo -e "${GREEN}==> $*${NC}"; }
warn()  { echo -e "${YELLOW}    WARNING: $*${NC}"; }
error() { echo -e "${RED}    ERROR: $*${NC}"; exit 1; }

# ── Shared: write sentinellog-ctl to /usr/local/bin ───────────────────────────
write_ctl() {
cat > "$CTL_BIN" <<'CTLSCRIPT'
#!/usr/bin/env bash
# =============================================================
# sentinellog-ctl  –  SentinelLog Service Manager
# Usage: sentinellog-ctl [status|start|stop|restart|logs] [service]
# =============================================================
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'
CYAN='\033[0;36m'; BOLD='\033[1m'; NC='\033[0m'

ALL_SERVICES=("sentinellog-agent" "syslog-ng" "syslog-postgres" "sentinellog-worker")

installed_services() {
    local found=()
    for svc in "${ALL_SERVICES[@]}"; do
        if systemctl list-unit-files --type=service 2>/dev/null | grep -q "^${svc}.service"; then
            found+=("$svc")
        fi
    done
    echo "${found[@]}"
}

svc_symbol() {
    systemctl is-active --quiet "$1" 2>/dev/null \
        && echo -e "${GREEN}● RUNNING${NC}" \
        || echo -e "${RED}✗ STOPPED${NC}"
}

cmd_status() {
    local svcs; read -ra svcs <<< "$(installed_services)"
    echo ""
    echo -e "${CYAN}${BOLD}  SentinelLog Services${NC}"
    echo "  ┌──────────────────────────────┬────────────────┐"
    for svc in "${svcs[@]}"; do
        printf "  │  %-28s│  " "$svc"
        echo -en "$(svc_symbol "$svc")"
        printf "  │\n"
    done
    echo "  └──────────────────────────────┴────────────────┘"
    echo ""
}

resolve_services() {
    local target="$1"
    local svcs; read -ra svcs <<< "$(installed_services)"
    local list=()
    if [ -n "$target" ]; then
        for svc in "${svcs[@]}"; do
            [[ "$svc" == *"$target"* ]] && list+=("$svc")
        done
        [ ${#list[@]} -eq 0 ] && { echo -e "${RED}No service matching '$target' found.${NC}" >&2; exit 1; }
    else
        list=("${svcs[@]}")
    fi
    echo "${list[@]}"
}

cmd_start() {
    local list; read -ra list <<< "$(resolve_services "$1")"
    for svc in "${list[@]}"; do
        echo -en "  Starting $svc ... "
        systemctl start "$svc" 2>/dev/null \
            && echo -e "${GREEN}OK${NC}" \
            || echo -e "${RED}FAILED${NC}"
    done
    echo ""; cmd_status
}

cmd_stop() {
    local list; read -ra list <<< "$(resolve_services "$1")"
    for svc in "${list[@]}"; do
        echo -en "  Stopping $svc ... "
        systemctl stop "$svc" 2>/dev/null \
            && echo -e "${GREEN}OK${NC}" \
            || echo -e "${RED}FAILED${NC}"
    done
    echo ""; cmd_status
}

cmd_restart() { cmd_stop "$1"; cmd_start "$1"; }

cmd_logs() {
    local target="${1:-agent}"
    local svcs; read -ra svcs <<< "$(installed_services)"
    local matched=""
    for svc in "${svcs[@]}"; do [[ "$svc" == *"$target"* ]] && matched="$svc" && break; done
    [ -z "$matched" ] && { echo -e "${RED}No service matching '$target'.${NC}"; exit 1; }
    echo -e "${CYAN}Logs for $matched  (Ctrl+C to exit)${NC}"; echo ""
    journalctl -u "$matched" -f --no-pager
}

cmd_help() {
    echo ""
    echo -e "${CYAN}${BOLD}  sentinellog-ctl – SentinelLog Service Manager${NC}"
    echo ""
    echo "  sentinellog-ctl status              Show all service states"
    echo "  sentinellog-ctl start  [service]    Start all or one service"
    echo "  sentinellog-ctl stop   [service]    Stop  all or one service"
    echo "  sentinellog-ctl restart[service]    Restart all or one"
    echo "  sentinellog-ctl logs   [service]    Tail logs (Ctrl+C to exit)"
    echo ""
    echo "  Service shortcuts (partial name match):"
    echo "    agent     →  sentinellog-agent"
    echo "    syslog    →  syslog-ng"
    echo "    postgres  →  syslog-postgres"
    echo "    worker    →  sentinellog-worker"
    echo ""
    echo "  Examples:"
    echo "    sentinellog-ctl status"
    echo "    sentinellog-ctl stop agent"
    echo "    sentinellog-ctl start worker"
    echo "    sentinellog-ctl restart"
    echo "    sentinellog-ctl logs receiver"
    echo ""
}

CMD="${1:-status}"; ARG="${2:-}"
case "$CMD" in
    status)         cmd_status ;;
    start)          cmd_start  "$ARG" ;;
    stop)           cmd_stop   "$ARG" ;;
    restart)        cmd_restart "$ARG" ;;
    logs)           cmd_logs   "$ARG" ;;
    help|--help|-h) cmd_help ;;
    *) echo -e "${RED}Unknown: $CMD${NC}"; cmd_help; exit 1 ;;
esac
CTLSCRIPT
chmod +x "$CTL_BIN"
echo "     Installed at: $CTL_BIN"
}

# =============================================================================
# MAIN
# =============================================================================

[[ $EUID -ne 0 ]] && error "Run as root:  sudo ./install.sh"

clear
echo -e "${CYAN}"
echo "  ╔══════════════════════════════════════════╗"
echo "  ║   SentinelLog Agent  –  Linux Installer  ║"
echo "  ╚══════════════════════════════════════════╝"
echo -e "${NC}"

# ── 1. .NET 8 runtime ─────────────────────────────────────────────────────────
info "1. Checking .NET 8 runtime..."
if dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.NETCore.App 8\."; then
    echo "     Already installed."
else
    if ls "$RUNTIME_DIR"/*.deb &>/dev/null 2>&1; then
        echo "     Installing from bundled .deb packages..."
        dpkg -i "$RUNTIME_DIR"/*.deb 2>/dev/null || true
        apt-get install -f -y --no-download 2>/dev/null || true
    elif ls "$RUNTIME_DIR"/*.rpm &>/dev/null 2>&1; then
        echo "     Installing from bundled .rpm packages..."
        rpm -ivh --nodeps "$RUNTIME_DIR"/*.rpm 2>/dev/null || true
    else
        error "No offline packages in dotnet-runtime/.  Run package.sh on an internet machine first."
    fi
    dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.NETCore.App 8\." \
        || error ".NET 8 install failed — check dotnet-runtime/ contents."
    echo "     .NET 8 OK."
fi

# ── 2. Validate binary ─────────────────────────────────────────────────────────
info "2. Checking agent binary..."
[ -d "$PUBLISH_DIR" ]              || error "publish/SyslogAgent not found."
[ -f "$PUBLISH_DIR/$BINARY_NAME" ] || error "Binary '$BINARY_NAME' missing in publish/SyslogAgent."
echo "     OK."

# ── 3. Configuration ───────────────────────────────────────────────────────────
info "3. Configuration"
echo ""
read -rp "     Syslog Receiver Host [127.0.0.1]: " SYSLOG_HOST
SYSLOG_HOST="${SYSLOG_HOST:-127.0.0.1}"

read -rp "     Syslog Receiver Port [514]: " SYSLOG_PORT
SYSLOG_PORT="${SYSLOG_PORT:-514}"

echo "     Protocol options: UDP  TCP  TLS"
read -rp "     Protocol [UDP]: " SYSLOG_PROTO
SYSLOG_PROTO="${SYSLOG_PROTO:-UDP}"
SYSLOG_PROTO="${SYSLOG_PROTO^^}"

echo ""
echo "     ┌─────────────────────────────────────────┐"
printf  "     │  Target: %-31s│\n" "$SYSLOG_HOST:$SYSLOG_PORT ($SYSLOG_PROTO)"
echo "     └─────────────────────────────────────────┘"
read -rp "     Proceed? [Y/n]: " CONFIRM
[[ "${CONFIRM,,}" == "n" ]] && { echo "Aborted."; exit 0; }
echo ""

# ── 4. Service user ────────────────────────────────────────────────────────────
info "4. Service user..."
if ! id "$SERVICE_USER" &>/dev/null; then
    useradd --system --no-create-home --shell /sbin/nologin "$SERVICE_USER"
    echo "     Created '$SERVICE_USER'."
else
    echo "     '$SERVICE_USER' already exists."
fi

# ── 5. Install files ───────────────────────────────────────────────────────────
info "5. Installing to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
cp -r "$PUBLISH_DIR"/. "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/$BINARY_NAME"
mkdir -p /var/lib/sentinellog-agent
chown -R "$SERVICE_USER:$SERVICE_USER" "$INSTALL_DIR" /var/lib/sentinellog-agent
echo "     Done."

# ── 6. Patch appsettings.json ──────────────────────────────────────────────────
info "6. Writing configuration..."
APPSETTINGS="$INSTALL_DIR/appsettings.json"
if [ -f "$APPSETTINGS" ]; then
    sed -i "s|\"Host\": \".*\"|\"Host\": \"$SYSLOG_HOST\"|g"               "$APPSETTINGS"
    sed -i "s|\"Port\": [0-9]*|\"Port\": $SYSLOG_PORT|g"                   "$APPSETTINGS"
    sed -i "s|\"Protocol\": \".*\"|\"Protocol\": \"$SYSLOG_PROTO\"|g"      "$APPSETTINGS"
    sed -i 's|"DeduplicationStorePath": ".*"|"DeduplicationStorePath": "/var/lib/sentinellog-agent/offsets.json"|g' "$APPSETTINGS"
    echo "     appsettings.json patched."
else
    warn "appsettings.json not found — creating minimal config."
    cat > "$APPSETTINGS" <<JSON
{
  "Agent": {
    "SyslogServer": {
      "Host": "$SYSLOG_HOST",
      "Port": $SYSLOG_PORT,
      "Protocol": "$SYSLOG_PROTO",
      "TcpTimeoutSeconds": 30,
      "MaxMessageBytes": 8192
    },
    "Collectors": {
      "ApplyFilter": false,
      "LinuxJournald": true,
      "LinuxBinaryLogs": true,
      "LinuxAutoDiscoverLogs": true
    },
    "BatchIntervalSeconds": 30,
    "EnableRealtime": true,
    "EnableBatch": true,
    "DeduplicationStorePath": "/var/lib/sentinellog-agent/offsets.json"
  },
  "Logging": { "LogLevel": { "Default": "Warning" } }
}
JSON
fi

# ── 7. Systemd service ─────────────────────────────────────────────────────────
info "7. Registering systemd service..."
cat > "/etc/systemd/system/$SERVICE_NAME.service" <<UNIT
[Unit]
Description=SentinelLog Agent
After=network.target

[Service]
Type=notify
User=$SERVICE_USER
WorkingDirectory=$INSTALL_DIR
ExecStart=$INSTALL_DIR/$BINARY_NAME
Restart=always
RestartSec=10
StartLimitBurst=5
StartLimitIntervalSec=60
Environment=DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

[Install]
WantedBy=multi-user.target
UNIT

systemctl daemon-reload
systemctl enable "$SERVICE_NAME" >/dev/null 2>&1
systemctl start  "$SERVICE_NAME" >/dev/null 2>&1 &
sleep 2
echo "     Service registered and starting in background."

# ── 8. Install sentinellog-ctl ─────────────────────────────────────────────────
info "8. Installing sentinellog-ctl..."
write_ctl

# ── Done ───────────────────────────────────────────────────────────────────────
echo ""
echo -e "${CYAN}"
echo "  ╔══════════════════════════════════════════════════╗"
echo "  ║   INSTALL COMPLETE                               ║"
echo "  ╠══════════════════════════════════════════════════╣"
printf "  ║  Service  : %-36s║\n" "$SERVICE_NAME"
printf "  ║  Target   : %-36s║\n" "$SYSLOG_HOST:$SYSLOG_PORT ($SYSLOG_PROTO)"
printf "  ║  Binary   : %-36s║\n" "$INSTALL_DIR/$BINARY_NAME"
echo "  ╠══════════════════════════════════════════════════╣"
echo "  ║  sentinellog-ctl status    ← check if running   ║"
echo "  ║  sentinellog-ctl stop      ← turn OFF           ║"
echo "  ║  sentinellog-ctl start     ← turn ON            ║"
echo "  ║  sentinellog-ctl logs      ← view live logs     ║"
echo "  ╚══════════════════════════════════════════════════╝"
echo -e "${NC}"
