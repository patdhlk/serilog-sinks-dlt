#!/usr/bin/env bash
# Starts dlt-daemon in the background, waits for its Unix socket, then exec's
# the user's command. Used as the container ENTRYPOINT.
#
# The daemon is built from COVESA source with WITH_DLT_UNIX_SOCKET_IPC=ON, so
# /tmp/dlt is a real AF_UNIX socket that our UnixSocketTransport can connect to.
set -euo pipefail

DLT_SOCKET="${DLT_SOCKET_PATH:-/tmp/dlt}"

# Set daemon mode = BOTH (3) so it writes to offline trace AND forwards to TCP
# clients. The default is EXTERNAL (1) which only forwards — nothing lands in
# /var/log/dlt. The daemon reads this file at startup; see dlt-daemon.c v3.0.0
# (LoggingMode in runtime cfg, not in dlt.conf).
echo "LoggingMode = 3" > /tmp/dlt-runtime.cfg

dlt-daemon -d -c /etc/dlt.conf

# Poll for the socket — daemon takes ~50 ms typically; bail after 5 s.
for _ in $(seq 1 50); do
    [[ -S "$DLT_SOCKET" ]] && break
    sleep 0.1
done

if [[ ! -S "$DLT_SOCKET" ]]; then
    echo "entrypoint: dlt-daemon socket $DLT_SOCKET did not appear within 5s" >&2
    echo "entrypoint: /tmp listing:" >&2
    ls -la /tmp/ >&2 || true
    exit 1
fi

exec "$@"
