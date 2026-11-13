using System.Globalization;
using FluentValidation;
using FluentValidation.AspNetCore;
using Prometheus;
using Serilog;
using Serilog.Enrichers.Span;
using UrlShortener.Application.Dto;
using UrlShortener.Application.Extensions;
using UrlShortener.Application.Health;
using UrlShortener.Application.Middleware;
using UrlShortener.Application.Settings;
using UrlShortener.Application.Validators;

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
    var builder = WebApplication.CreateBuilder(new WebApplicationOptions
    {
        Args = args,
        // SharedConfig/appsettings.json is linked into the output folder. Using it as the content root
        // makes `dotnet run`, tests and the Docker image load the same file, with the standard override
        // order on top: appsettings.{Environment}.json, user-secrets (Development), environment variables.
        ContentRootPath = AppContext.BaseDirectory
    });
    builder.Host.UseSerilog();

    builder.Services.AddValidatedSettings<AppSettings>(builder.Configuration, AppSettings.SectionName);
    builder.Services.AddValidatedSettings<RateLimitingSettings>(builder.Configuration, RateLimitingSettings.SectionName);
    builder.Services.AddPersistence(builder.Configuration);
    builder.Services.AddMessagingServices(builder.Configuration);
    builder.Services.AddAppServices();
    builder.Services.AddResiliencePolicies();
    builder.Services.AddTelemetry(builder.Configuration, builder.Environment);
    builder.Services.AddDependencyHealthChecks();
    // Unhandled errors become RFC 7807 problem responses without stack traces.
    builder.Services.AddProblemDetails();
    builder.Services.AddControllers();
    builder.Services.AddSwaggerGen();
    builder.Services.AddScoped<IValidator<UrlRequestDto>, UrlRequestDtoValidator>();
    builder.Services.AddFluentValidationAutoValidation();

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseRouting();
    app.UseHttpMetrics();
    app.UseWhen(context => HttpMethods.IsPost(context.Request.Method) &&
                           context.Request.Path.StartsWithSegments("/api/shorten"),
        appBuilder => appBuilder.UseMiddleware<RateLimitingMiddleware>());
    app.MapMonitoringEndpoints();
    app.MapControllers();
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application failed to start");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
