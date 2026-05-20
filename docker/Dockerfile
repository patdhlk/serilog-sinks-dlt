FROM mcr.microsoft.com/dotnet/sdk:9.0-noble

# Build dlt-daemon from source with Unix-socket IPC.
# Ubuntu's apt build uses FIFO IPC, which our sink does not speak; the source
# build with WITH_DLT_UNIX_SOCKET_IPC=ON gives a daemon that listens on a
# real AF_UNIX socket at /tmp/dlt.
ARG DLT_DAEMON_VERSION=v3.0.0

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
        build-essential \
        cmake \
        git \
        zlib1g-dev \
        ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && git clone --depth 1 --branch ${DLT_DAEMON_VERSION} https://github.com/COVESA/dlt-daemon.git /tmp/dlt-daemon \
    # Strip -Werror from the project's flags — modern gcc emits a sign-conversion
    # warning that v2.18.x/v3.0 hasn't fixed; we don't want it as a build error.
    && find /tmp/dlt-daemon -name CMakeLists.txt -exec sed -i 's/-Werror/-Wno-error/g' {} + \
    && cmake -S /tmp/dlt-daemon -B /tmp/dlt-daemon/build \
        -DCMAKE_BUILD_TYPE=Release \
        -DCMAKE_INSTALL_PREFIX=/usr/local \
        -DDLT_IPC=UNIX_SOCKET \
        -DWITH_DLT_USE_IPv6=OFF \
        -DBUILD_SHARED_LIBS=ON \
        -DWITH_DLT_TESTS=OFF \
        -DWITH_DLT_EXAMPLES=ON \
    && cmake --build /tmp/dlt-daemon/build -j"$(nproc)" \
    && cmake --install /tmp/dlt-daemon/build \
    && ldconfig \
    && rm -rf /tmp/dlt-daemon \
    && mkdir -p /var/log/dlt \
    && chmod 1777 /var/log/dlt

COPY docker/dlt.conf      /etc/dlt.conf
COPY docker/entrypoint.sh /usr/local/bin/entrypoint.sh
COPY docker/demo.sh       /usr/local/bin/demo.sh
RUN chmod +x /usr/local/bin/entrypoint.sh /usr/local/bin/demo.sh

ENV DLT_DAEMON_AVAILABLE=1 \
    DLT_SOCKET_PATH=/tmp/dlt \
    DLT_TRACE_DIR=/var/log/dlt \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

WORKDIR /workspace

ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
CMD ["bash"]
