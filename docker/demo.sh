#!/usr/bin/env bash
# End-to-end demo inside the dev container:
#   1. Build samples/Demo.
#   2. Run it — writes DLT-format messages to /tmp/demo.dlt AND streams to
#      the running dlt-daemon (sample registers as APID "DEMO").
#   3. Decode /tmp/demo.dlt with dlt-convert — proves our encoder produces
#      bytes that COVESA's reference tooling can read.
#   4. Show daemon log + offline trace state for transparency.
#
# Run from the container as: demo.sh
# (Requires the repo bind-mounted at /workspace.)
set -euo pipefail

cd /workspace

# Place outputs under the bind-mounted /workspace so the user can open them
# in dlt-viewer ON THE HOST without copying anything out of the container.
mkdir -p /workspace/.demo-output
SAMPLE_FILE=/workspace/.demo-output/demo.dlt
DAEMON_TRACE_DIR=/workspace/.demo-output/daemon-trace
mkdir -p "$DAEMON_TRACE_DIR"

echo "==> dotnet build (Release)"
dotnet build -c Release --nologo 2>&1 | tail -3

echo ""
echo "==> Running samples/Demo (writes file + streams to dlt-daemon)"
dotnet run --project samples/Demo --no-build -c Release -- "$SAMPLE_FILE"

echo ""
echo "==> dlt-convert decode of the sample's .dlt file:"
dlt-convert -a "$SAMPLE_FILE" | head -15

echo ""
echo "==> dlt-daemon offline trace (live ingestion):"
if compgen -G "/var/log/dlt/*.dlt" > /dev/null; then
    # Copy the daemon's offline traces into the bind mount so the host can
    # open them in dlt-viewer too.
    cp /var/log/dlt/*.dlt "$DAEMON_TRACE_DIR/"
    dlt-convert -a /var/log/dlt/*.dlt | head -15
else
    echo "(empty — see docker/README.md)"
fi

echo ""
echo "==> Done. Files for dlt-viewer (on the HOST, in this repo):"
echo "    .demo-output/demo.dlt           — file sink output"
ls "$DAEMON_TRACE_DIR"/*.dlt 2>/dev/null \
    | sed "s|$DAEMON_TRACE_DIR/|    .demo-output/daemon-trace/|" \
    || true
echo ""
echo "    Open either in DLT Viewer:  File -> Open DLT File..."
