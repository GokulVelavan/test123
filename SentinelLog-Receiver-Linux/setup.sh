#!/usr/bin/env bash
# =============================================================================
# SentinelLog Receiver – Linux Installer
# Usage:  sudo ./setup.sh
# Installs Syslog-NG + PostgreSQL forwarder from offline-packages/
# PostgreSQL must already be running and accessible on the network.
# =============================================================================
set -e

CTL_BIN="/usr/local/bin/sentinellog-ctl"

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
    local target="${1:-syslog}"
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
    echo "    sentinellog-ctl stop syslog"
    echo "    sentinellog-ctl start postgres"
    echo "    sentinellog-ctl restart"
    echo "    sentinellog-ctl logs syslog"
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

TLS_PORT="6514"
CERT_DIR="/etc/syslog-ng/cert.d"
KEY_FILE="$CERT_DIR/server.key"
CERT_FILE="$CERT_DIR/server.crt"
LOGFILE="/var/log/syslog-ng-postgres.log"
DB_CONF_FILE="/etc/syslog-ng/db.conf"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PKG_DIR="$SCRIPT_DIR/offline-packages"

clear
echo -e "${CYAN}"
echo "  ╔══════════════════════════════════════════════╗"
echo "  ║   SentinelLog Receiver  –  Linux Installer   ║"
echo "  ╚══════════════════════════════════════════════╝"
echo -e "${NC}"

# ── 1. Prompts ─────────────────────────────────────────────────────────────────
info "1. Configuration"
echo ""

read -rp "     PostgreSQL Host [127.0.0.1]: " SQL_HOST
SQL_HOST="${SQL_HOST:-127.0.0.1}"

read -rp "     PostgreSQL Port [5432]: " SQL_PORT
SQL_PORT="${SQL_PORT:-5432}"

read -rp "     Database Name  [Syslog]: " SQL_DB
SQL_DB="${SQL_DB:-Syslog}"
if ! [[ "$SQL_DB" =~ ^[a-zA-Z_][a-zA-Z0-9_]*$ ]]; then
    error "Invalid database name — letters, numbers, underscores only."
fi

read -rp "     PostgreSQL User [postgres]: " SQL_USER
SQL_USER="${SQL_USER:-postgres}"

read -srp "     PostgreSQL Password: " SQL_PASS
echo ""

SYSLOG_IP=$(hostname -I | awk '{print $1}')
read -rp "     This server's IP for TLS cert [$SYSLOG_IP]: " CERT_CN
CERT_CN="${CERT_CN:-$SYSLOG_IP}"

echo ""
echo "     ┌─────────────────────────────────────────────┐"
printf  "     │  DB Host  : %-31s│\n" "$SQL_HOST:$SQL_PORT"
printf  "     │  Database : %-31s│\n" "$SQL_DB"
printf  "     │  TLS CN   : %-31s│\n" "$CERT_CN"
echo    "     └─────────────────────────────────────────────┘"
read -rp "     Proceed? [Y/n]: " CONFIRM
[[ "${CONFIRM,,}" == "n" ]] && { echo "Aborted."; exit 0; }
echo ""

# ── 2. Install packages ────────────────────────────────────────────────────────
info "2. Installing packages (offline)..."
systemctl stop syslog-ng syslog-postgres 2>/dev/null || true

SCL_MISSING=false
if command -v syslog-ng &>/dev/null && command -v python3 &>/dev/null; then
    echo "     Packages already installed — skipping."
    if ! dpkg -s syslog-ng-scl &>/dev/null 2>&1 || \
       { [ ! -f /etc/syslog-ng/scl.conf ] && [ ! -f /usr/share/syslog-ng/include/scl.conf ]; }; then
        SCL_MISSING=true
    fi
elif [ -d "$PKG_DIR" ] && ls "$PKG_DIR"/*.deb &>/dev/null 2>&1; then
    dpkg -i "$PKG_DIR"/*.deb 2>/dev/null || true
    apt-get install -f -y 2>/dev/null || true
    for pkg in syslog-ng python3 openssl; do
        command -v "$pkg" &>/dev/null || error "$pkg failed to install — check offline-packages/."
    done
    if dpkg -s syslog-ng-scl &>/dev/null 2>&1 && \
       { [ -f /etc/syslog-ng/scl.conf ] || [ -f /usr/share/syslog-ng/include/scl.conf ]; }; then
        SCL_MISSING=false
    else
        warn "syslog-ng-scl not available — continuing without SCL."
        SCL_MISSING=true
    fi
    echo "     Packages installed OK."
else
    error "offline-packages/ not found and packages not installed. Re-run package.sh first."
fi

# ── 3. Firewall ────────────────────────────────────────────────────────────────
info "3. Configuring firewall..."
if command -v ufw &>/dev/null; then
    ufw allow 514/udp  >/dev/null 2>&1
    ufw allow 1514/udp >/dev/null 2>&1
    ufw allow 601/tcp  >/dev/null 2>&1
    ufw allow 1515/tcp >/dev/null 2>&1
    ufw allow 6514/tcp >/dev/null 2>&1
    echo "     UFW rules added (514/udp 1514/udp 601/tcp 1515/tcp 6514/tcp)."
elif command -v firewall-cmd &>/dev/null; then
    firewall-cmd --permanent --add-port=514/udp  >/dev/null 2>&1
    firewall-cmd --permanent --add-port=1514/udp >/dev/null 2>&1
    firewall-cmd --permanent --add-port=601/tcp  >/dev/null 2>&1
    firewall-cmd --permanent --add-port=1515/tcp >/dev/null 2>&1
    firewall-cmd --permanent --add-port=6514/tcp >/dev/null 2>&1
    firewall-cmd --reload >/dev/null 2>&1
    echo "     firewalld rules added."
else
    warn "No firewall manager found — open ports 514/601/1514/1515/6514 manually."
fi

# ── 4. TLS certificate ─────────────────────────────────────────────────────────
info "4. Setting up TLS certificate..."
mkdir -p "$CERT_DIR"
if [ ! -f "$KEY_FILE" ]; then
    openssl req -x509 -newkey rsa:4096 \
        -keyout "$KEY_FILE" -out "$CERT_FILE" \
        -days 3650 -nodes \
        -subj "/CN=$CERT_CN" \
        -addext "subjectAltName=IP:$CERT_CN" \
        >/dev/null 2>&1
    echo "     TLS cert generated (10 year): $CERT_FILE"
else
    echo "     TLS cert already exists — skipping."
fi
chmod 600 "$KEY_FILE"

# ── 5. Syslog-NG config ────────────────────────────────────────────────────────
info "5. Writing syslog-ng configuration..."
cat > /etc/syslog-ng/syslog-ng.conf <<SYSLOGCONF
@version: current
SCLINCLUDE

options {
    keep-hostname(yes);
    chain-hostnames(no);
    flush-lines(1);
    perm(0640);
};

source s_net {
    network(ip("0.0.0.0") port(514)  transport("udp"));
    network(ip("0.0.0.0") port(1514) transport("udp"));
    network(ip("0.0.0.0") port(601)  transport("tcp"));
    network(ip("0.0.0.0") port(1515) transport("tcp"));
    network(ip("0.0.0.0") port(TLSPORT transport("tls")
        tls(key-file("KEYFILE") cert-file("CERTFILE") peer-verify(optional-untrusted))
    );
};

parser p_pid_extractor {
    csv-parser(
        columns("PROGRAM", "PID")
        delimiters("[]")
        flags(greedy)
        template("\${PROGRAM}")
    );
};

rewrite r_format_pid {
    set("\${PID:- -}" value("PID"));
};

template t_db_mapping {
    template("\${ISODATE}|\${HOST}|\${PROGRAM}|\${PID}|\${SOURCEIP}|\${MESSAGE}\n");
};

destination d_file { file("LOGFILEPATH" template(t_db_mapping)); };

log {
    source(s_net);
    if (match('\[[0-9]+\]' value("PROGRAM"))) { parser(p_pid_extractor); };
    rewrite(r_format_pid);
    destination(d_file);
};
SYSLOGCONF

if [ "$SCL_MISSING" = true ]; then
    sed -i "s|SCLINCLUDE||g" /etc/syslog-ng/syslog-ng.conf
else
    sed -i 's|SCLINCLUDE|@include "scl.conf"|g' /etc/syslog-ng/syslog-ng.conf
fi
sed -i "s|TLSPORT|${TLS_PORT})|g"    /etc/syslog-ng/syslog-ng.conf
sed -i "s|KEYFILE|${KEY_FILE}|g"      /etc/syslog-ng/syslog-ng.conf
sed -i "s|CERTFILE|${CERT_FILE}|g"    /etc/syslog-ng/syslog-ng.conf
sed -i "s|LOGFILEPATH|${LOGFILE}|g"   /etc/syslog-ng/syslog-ng.conf

touch "$LOGFILE"
chmod 640 "$LOGFILE"

systemctl restart syslog-ng >/dev/null 2>&1 &
sleep 2
echo "     syslog-ng starting in background."

# ── 6. Store DB credentials ────────────────────────────────────────────────────
info "6. Storing database credentials..."
cat > "$DB_CONF_FILE" <<EOF
SQL_HOST=${SQL_HOST}
SQL_PORT=${SQL_PORT}
SQL_DB=${SQL_DB}
SQL_USER=${SQL_USER}
SQL_PASS=${SQL_PASS}
EOF
chmod 600 "$DB_CONF_FILE"
chown root:root "$DB_CONF_FILE"
echo "     Stored at $DB_CONF_FILE (permissions: 600)."

# ── 7. Test DB connection ──────────────────────────────────────────────────────
info "7. Testing database connection..."
export SQL_HOST SQL_PORT SQL_DB SQL_USER SQL_PASS
python3 - <<'PYEOF'
import psycopg2, sys, os
try:
    conn = psycopg2.connect(
        host=os.environ["SQL_HOST"], port=os.environ["SQL_PORT"],
        dbname=os.environ["SQL_DB"], user=os.environ["SQL_USER"],
        password=os.environ["SQL_PASS"], sslmode="disable"
    )
    conn.close()
    print("     Database connection OK.")
except Exception as e:
    print(f"     ERROR: Cannot connect: {e}")
    print("     Make sure init-db.sql has been run on the database server.")
    sys.exit(1)
PYEOF

# ── 8. Log forwarder (Python service) ─────────────────────────────────────────
info "8. Installing log forwarder service..."
cat > /usr/local/bin/syslog-tail-postgres.py <<'PYEOF'
#!/usr/bin/env python3
import psycopg2, time, os, sys
from datetime import datetime

LOGFILE = os.environ.get("SYSLOG_LOGFILE", "/var/log/syslog-ng-postgres.log")
sys.stdout.reconfigure(line_buffering=True)

def log(msg):
    print(f"[{datetime.now().strftime('%Y-%m-%d %H:%M:%S')}] {msg}")

def get_db_conf():
    conf = {}
    with open(os.environ.get("DB_CONF_FILE", "/etc/syslog-ng/db.conf")) as f:
        for line in f:
            line = line.strip()
            if "=" in line:
                k, v = line.split("=", 1)
                conf[k.strip()] = v.strip()
    return conf

def run():
    conf = get_db_conf()
    conn = None; cur = None; count = 0
    while True:
        try:
            if conn is None or conn.closed:
                conf = get_db_conf()
                log(f"Connecting to {conf['SQL_HOST']}:{conf['SQL_PORT']}/{conf['SQL_DB']}")
                conn = psycopg2.connect(
                    host=conf["SQL_HOST"], port=conf["SQL_PORT"],
                    dbname=conf["SQL_DB"], user=conf["SQL_USER"],
                    password=conf["SQL_PASS"], sslmode="disable"
                )
                cur = conn.cursor()
                log("DB connected. Waiting for logs...")
            with open(LOGFILE, 'r') as f:
                f.seek(0, 2)
                while True:
                    line = f.readline()
                    if not line:
                        time.sleep(0.1)
                        continue
                    parts = line.strip().split('|', 5)
                    if len(parts) == 6:
                        p_pid = parts[3].strip('[]: -')
                        p_pid = p_pid if p_pid and p_pid.isdigit() else '0'
                        cur.execute(
                            "INSERT INTO public.systemlogs (log_datetime,host,program,pid,source_ip,message) VALUES (%s,%s,%s,%s,%s,%s)",
                            (parts[0], parts[1], parts[2], p_pid, parts[4], parts[5])
                        )
                        conn.commit()
                        count += 1
                        log(f"SAVED #{count} | from={parts[4]} host={parts[1]} prog={parts[2]} | {parts[5][:80]}")
                    else:
                        log(f"SKIP malformed: {line.strip()[:100]}")
        except Exception as e:
            log(f"ERROR: {e} — reconnecting in 5s...")
            try:
                if cur:  cur.close()
                if conn: conn.close()
            except: pass
            conn = None; cur = None
            time.sleep(5)

run()
PYEOF
chmod 700 /usr/local/bin/syslog-tail-postgres.py

cat > /etc/systemd/system/syslog-postgres.service <<EOF
[Unit]
Description=SentinelLog – Syslog to Postgres Forwarder
After=syslog-ng.service

[Service]
ExecStart=/usr/bin/python3 /usr/local/bin/syslog-tail-postgres.py
Environment=SYSLOG_LOGFILE=${LOGFILE}
Environment=DB_CONF_FILE=${DB_CONF_FILE}
Restart=always
RestartSec=5
StartLimitBurst=10
StartLimitIntervalSec=60

[Install]
WantedBy=multi-user.target
EOF

systemctl daemon-reload
systemctl enable syslog-postgres >/dev/null 2>&1
systemctl enable syslog-ng       >/dev/null 2>&1
systemctl start  syslog-postgres >/dev/null 2>&1 &
sleep 2
echo "     syslog-postgres starting in background."

# ── 9. Install sentinellog-ctl ─────────────────────────────────────────────────
info "9. Installing sentinellog-ctl..."
write_ctl

# ── Done ───────────────────────────────────────────────────────────────────────
echo ""
echo -e "${CYAN}"
echo "  ╔══════════════════════════════════════════════════╗"
echo "  ║   INSTALL COMPLETE                               ║"
echo "  ╠══════════════════════════════════════════════════╣"
printf "  ║  DB Host  : %-36s║\n" "$SQL_HOST:$SQL_PORT"
printf "  ║  Database : %-36s║\n" "$SQL_DB"
printf "  ║  TLS Cert : %-36s║\n" "$CERT_FILE"
printf "  ║  Log File : %-36s║\n" "$LOGFILE"
echo "  ╠══════════════════════════════════════════════════╣"
echo "  ║  sentinellog-ctl status    ← check if running   ║"
echo "  ║  sentinellog-ctl stop      ← turn OFF           ║"
echo "  ║  sentinellog-ctl start     ← turn ON            ║"
echo "  ║  sentinellog-ctl logs      ← view live logs     ║"
echo "  ╚══════════════════════════════════════════════════╝"
echo ""
echo "  TLS cert for agents: copy $CERT_FILE to agents"
echo "                       or use peer-verify(optional-untrusted)"
echo -e "${NC}"
