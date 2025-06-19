using Serilog;
using Prometheus;
using Serilog.Enrichers.Span;
using UrlShortener.Infrastructure.Extensions;
using FluentValidation.AspNetCore;
using FluentValidation;
using UrlShortener.Application.Dto;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} | TraceId={TraceId} SpanId={SpanId}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

var sharedConfigPath = Path.GetFullPath(Path.Combine("..", "SharedConfig", "appsettings.json"));
builder.Configuration
    .AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddMessagingServices(builder.Configuration);
builder.Services.AddAppServices();
builder.Services.AddResiliencePolicies();
builder.Services.AddHealthChecks();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddTelemetry(builder.Configuration);
builder.Services.AddScoped<IValidator<UrlRequestDto>, UrlRequestDtoValidator>();
builder.Services.AddFluentValidationAutoValidation();

var app = builder.Build();

app.UseMiddleware<MetricsMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpMetrics();
app.UseRouting();
app.UseAuthorization();
app.MapMonitoringEndpoints();
app.MapControllers();
app.Run();
