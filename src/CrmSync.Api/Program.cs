using System.Text.Json.Serialization;
using CrmSync.Api;
using CrmSync.Api.Data;
using CrmSync.Api.Sync;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Resolved when the context is created (not here) so configuration added by tests is honored.
// The database file lives under the API's content root unless configured otherwise.
builder.Services.AddDbContext<SyncDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var environment = services.GetRequiredService<IHostEnvironment>();
    var databasePath = Path.GetFullPath(Path.Combine(environment.ContentRootPath, configuration["Database:Path"] ?? "App_Data/crmsync.db"));
    Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
    options.UseSqlite($"Data Source={databasePath}");
});
builder.Services.Configure<DemoOptions>(builder.Configuration.GetSection("Demo"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<SyncService>();
builder.Services.AddScoped<DemoSeeder>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/health");
app.MapSyncApi();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<DemoSeeder>().EnsureSeeded();
}

app.Run();

/// <summary>Exposed so integration tests can host the app with WebApplicationFactory.</summary>
public partial class Program;
