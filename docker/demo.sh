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

echo "==> dotnet build (Release)"
dotnet build -c Release --nologo 2>&1 | tail -3

echo ""
echo "==> Running samples/Demo (writes file + streams to dlt-daemon)"
dotnet run --project samples/Demo --no-build -c Release -- /tmp/demo.dlt

echo ""
echo "==> dlt-convert -a /tmp/demo.dlt — decoded view of what our sink produced:"
dlt-convert -a /tmp/demo.dlt | head -20
echo ""
echo "==> /tmp/demo.dlt size: $(stat -c%s /tmp/demo.dlt) bytes"

echo ""
echo "==> dlt-daemon offline trace (note: see README for the bidirectional gap):"
if compgen -G "/var/log/dlt/*.dlt" > /dev/null; then
    dlt-convert -a /var/log/dlt/*.dlt | head -10
else
    echo "(empty — daemon ingestion not yet wired through; see docker/README.md)"
fi

echo ""
echo "==> demo.sh complete"
