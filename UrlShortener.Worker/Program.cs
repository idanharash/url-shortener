using System.Globalization;
using Serilog;
using Serilog.Enrichers.Span;
using UrlShortener.Worker;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | TraceId={TraceId} SpanId={SpanId}{NewLine}{Exception}",
        formatProvider: CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var builder = WorkerHost.CreateBuilder(args);
    builder.Services.AddSerilog();
    await builder.Build().RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
