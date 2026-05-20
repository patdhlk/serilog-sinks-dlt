using Serilog;

var path = args.Length > 0 ? args[0] : "demo.dlt";

using var log = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.DltFile(path, appId: "DEMO", ecuId: "ECU1")
    .CreateLogger();

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

Console.WriteLine($"Wrote DLT messages to {path}. Open with dlt-viewer.");
