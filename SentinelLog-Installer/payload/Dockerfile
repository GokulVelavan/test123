FROM ubuntu:22.04
ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update && apt-get install -y \
    syslog-ng \
    python3 \
    python3-psycopg2 \
    openssl \
    && apt-get clean && rm -rf /var/lib/apt/lists/*

COPY syslog-ng.conf  /etc/syslog-ng/syslog-ng.conf
COPY mssql.py        /etc/syslog-ng/mssql.py

RUN mkdir -p /etc/syslog-ng/certs

ENV PYTHONPATH=/etc/syslog-ng

# UDP 514, TCP 514 (same port), TLS 6514
EXPOSE 514/udp 514/tcp 6514/tcp

CMD ["syslog-ng", "--foreground", "--no-caps", "-f", "/etc/syslog-ng/syslog-ng.conf"]
