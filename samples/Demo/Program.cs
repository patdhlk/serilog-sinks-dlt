using Serilog;

var path = args.Length > 0 ? args[0] : "demo.dlt";
var daemonAvailable = Environment.GetEnvironmentVariable("DLT_DAEMON_AVAILABLE") == "1";

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.DltFile(path, appId: "DEMO", ecuId: "ECU1");

if (daemonAvailable)
{
    var socketPath = Environment.GetEnvironmentVariable("DLT_SOCKET_PATH") ?? "/tmp/dlt";
    loggerConfig = loggerConfig.WriteTo.Dlt(appId: "DEMO", socketPath: socketPath);
    Console.WriteLine($"Also streaming to dlt-daemon at {socketPath}");
}

using var log = loggerConfig.CreateLogger();

for (var i = 0; i < 10; i++)
{
    log.Information("loop iteration {i} of {total} flag={flag}", i, 10, i % 2 == 0);
}

log.Warning("something happened: {payload}", new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

try
{
    throw new InvalidOperationException("simulated");
}
catch (Exception ex)
{
    log.Error(ex, "caught a {kind} exception", "simulated");
}

if (daemonAvailable)
{
    // Give dlt-daemon time to process registrations + read pending log bytes
    // before the sink closes the socket. The daemon's send-back messages
    // (LOG_STATE/LOG_LEVEL) need our socket open to land.
    await Task.Delay(2000);
}

Console.WriteLine($"Wrote DLT messages to {path}. Open with dlt-viewer.");
