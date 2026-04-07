# Syslog-NG OSE + PostgreSQL Setup (Offline)

## Files

| File | Purpose |
|------|---------|
| `init-db.sql` | Run on PostgreSQL server to create database and table |
| `setup.sh` | Run on Linux machine to install syslog-ng (offline, no internet) |
| `offline-packages/` | Pre-downloaded .deb packages for Ubuntu 22.04 |

## Steps

### 1. Setup database (PostgreSQL server)

**Using psql:**
```
psql -U postgres -f init-db.sql
```

**Using pgAdmin:**
1. Right-click Databases > Create > Database > Name: `syslogs` > Save
2. Click on `syslogs` database > Open Query Tool
3. Copy-paste the CREATE TABLE and CREATE INDEX statements from `init-db.sql` and run

**Important:** Make sure PostgreSQL allows remote connections. See instructions at the top of `init-db.sql`.

### 2. Copy folder to Linux machine

Copy the entire `Linux-Syslog` folder to the Linux machine via pendrive or shared folder.

### 3. Run setup (Linux machine - no internet needed)

```bash
cd "/path/to/Linux-Syslog"
sudo bash setup.sh
```

It will ask for:
- PostgreSQL Host IP
- Port (default: 5432)
- Database name (default: syslogs)
- Username (default: postgres)
- Password
- This server's IP for TLS cert

### 4. Test

Send a test log:
```bash
logger -n 127.0.0.1 -P 514 -t 'TestApp[1234]' 'Hello from syslog test'
```

Send a test log on different ports:
```bash
logger -n 127.0.0.1 -P 514 -t 'PowerShell[9999]' 'UDP 514 test'
logger -n 127.0.0.1 -P 1514 -t 'NXLog[5678]' 'UDP 1514 test'
logger -T -n 127.0.0.1 -P 601 -t 'Firewall[100]' 'TCP 601 test'
logger -T -n 127.0.0.1 -P 1515 -t 'AppServer[200]' 'TCP 1515 test'
```

### 5. Check Status

Watch forwarder logs live (shows each log being saved to DB):
```bash
journalctl -u syslog-postgres -f
```

Check syslog-ng service status:
```bash
sudo systemctl status syslog-ng
```

Check forwarder service status:
```bash
sudo systemctl status syslog-postgres
```

Check raw log file:
```bash
tail -f /var/log/syslog-ng-postgres.log
```

### 6. Verify in Database

Open pgAdmin or psql and run:
```sql
SELECT * FROM public.systemlogs ORDER BY id DESC LIMIT 10;
```
