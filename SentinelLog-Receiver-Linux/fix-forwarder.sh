#!/usr/bin/env bash
set -e

cat > /usr/local/bin/syslog-tail-postgres.py << 'PYEOF'
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
    conn = None
    cur = None
    count = 0
    while True:
        try:
            if conn is None or conn.closed:
                conf = get_db_conf()
                log(f"Connecting to DB: {conf['SQL_HOST']}:{conf['SQL_PORT']}/{conf['SQL_DB']}")
                conn = psycopg2.connect(
                    host=conf["SQL_HOST"],
                    port=conf["SQL_PORT"],
                    dbname=conf["SQL_DB"],
                    user=conf["SQL_USER"],
                    password=conf["SQL_PASS"],
                    sslmode="disable"
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
                            "INSERT INTO public.systemlogs (log_datetime, host, program, pid, source_ip, message) VALUES (%s,%s,%s,%s,%s,%s)",
                            (parts[0], parts[1], parts[2], p_pid, parts[4], parts[5])
                        )
                        conn.commit()
                        count += 1
                        log(f"SAVED #{count} | from={parts[4]} host={parts[1]} program={parts[2]} pid={p_pid} | {parts[5][:80]}")
                    else:
                        log(f"SKIPPED malformed line: {line.strip()[:100]}")
        except Exception as e:
            log(f"ERROR: {e} — reconnecting in 5s...")
            if cur:
                try: cur.close()
                except: pass
            if conn:
                try: conn.close()
                except: pass
            conn = None
            cur = None
            time.sleep(5)

run()
PYEOF

chmod 700 /usr/local/bin/syslog-tail-postgres.py
systemctl restart syslog-postgres
echo "Done. Watching logs..."
journalctl -u syslog-postgres -f
