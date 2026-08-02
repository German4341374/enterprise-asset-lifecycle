using System.Text.Json.Serialization;
using EnterpriseAssetLifecycle.Api;
using EnterpriseAssetLifecycle.Infrastructure;
using EnterpriseAssetLifecycle.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddRazorPages();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<WarrantyMonitorOptions>(builder.Configuration.GetSection("WarrantyMonitor"));
builder.Services.AddDbContext<AssetDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Database")));
builder.Services.AddScoped<AssetLifecycleService>();
builder.Services.AddScoped<AssetQueryService>();
builder.Services.AddScoped<AssetCsvService>();
builder.Services.AddHostedService<WarrantyMonitorService>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("postgresql", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseStaticFiles();
app.UseRouting();

app.MapOpenApi();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapAssetApi();
app.MapRazorPages();

await using (var scope = app.Services.CreateAsyncScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
    await database.Database.MigrateAsync();
    await SeedData.InitializeAsync(database);
}

await app.RunAsync();

public partial class Program;
