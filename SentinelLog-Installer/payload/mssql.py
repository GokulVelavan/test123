import psycopg2, time, os

SERVER = os.environ.get("SQL_HOST", "127.0.0.1")
PORT   = int(os.environ.get("SQL_PORT", "5432"))
USER   = os.environ.get("SQL_USER", "sysloguser")
PASS   = os.environ.get("SQL_PASS", "Syslog@123")
DB     = os.environ.get("SQL_DB",   "syslogdb")

MAX_CONNECT_RETRIES = 60  # 60 * 5 = 300 seconds max wait

class MSSQL:
    def __init__(self):
        self.conn = None
        self.cur = None

    def init(self, options):
        self._connect()
        return True

    def _connect(self):
        retries = 0
        while retries < MAX_CONNECT_RETRIES:
            try:
                self.conn = psycopg2.connect(
                    host=SERVER, port=PORT,
                    user=USER, password=PASS,
                    dbname=DB, connect_timeout=5
                )
                self.conn.autocommit = True
                self.cur = self.conn.cursor()
                print(f"[postgres] Connected to {SERVER}/{DB}", flush=True)
                return
            except Exception as e:
                retries += 1
                print(f"[postgres] Connect failed (attempt {retries}/{MAX_CONNECT_RETRIES}): {e}", flush=True)
                if retries >= MAX_CONNECT_RETRIES:
                    print(f"[postgres] Max retries reached. Giving up.", flush=True)
                    raise
                time.sleep(5)

    def send(self, msg):
        try:
            def safe_get(key, default):
                try:
                    return str(msg[key])
                except (KeyError, TypeError):
                    return default

            source_ip = safe_get("SOURCEIP", "0.0.0.0")[:50]
            host      = safe_get("HOST", "unknown")[:255]
            program   = safe_get("PROGRAM", "unknown")[:255]
            pid       = safe_get("PID", "0")[:50]
            message   = str(msg["MESSAGE"])

            self.cur.execute("""
                INSERT INTO public.systemlogs
                (log_datetime, host, program, pid, message, source_ip)
                VALUES (NOW(), %s, %s, %s, %s, %s)
            """, (host, program, pid, message, source_ip))

            return True

        except Exception as e:
            print(f"[postgres] Error during insert: {e}", flush=True)
            if "closed" in str(e).lower() or "connection" in str(e).lower():
                self._connect()
            return False

    def open(self): return True
    def close(self): pass
    def is_opened(self): return True
    def deinit(self):
        if self.conn: self.conn.close()
