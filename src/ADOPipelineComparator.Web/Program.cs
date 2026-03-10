using ADOPipelineComparator.Core.Interfaces;
using ADOPipelineComparator.Core.Services;
using ADOPipelineComparator.Data;
using ADOPipelineComparator.Data.Repositories;
using ADOPipelineComparator.Web.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var encryptionKey = Environment.GetEnvironmentVariable("ENCRYPTION_KEY")
    ?? builder.Configuration["EncryptionKey"];

if (string.IsNullOrWhiteSpace(encryptionKey))
{
    throw new InvalidOperationException("Encryption key is missing. Set ENCRYPTION_KEY or appsettings:EncryptionKey.");
}

var configuredDbPath = Environment.GetEnvironmentVariable("DB_PATH")
    ?? builder.Configuration["Database:Path"];

if (string.IsNullOrWhiteSpace(configuredDbPath))
{
    var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    configuredDbPath = Path.Combine(appDataPath, "ADOPipelineComparator", "data.db");
}

configuredDbPath = Environment.ExpandEnvironmentVariables(configuredDbPath);
var fullDbPath = Path.GetFullPath(configuredDbPath);
var dbDirectory = Path.GetDirectoryName(fullDbPath)
    ?? throw new InvalidOperationException("Invalid database path.");

Directory.CreateDirectory(dbDirectory);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={fullDbPath}"));

builder.Services.AddSingleton<IEncryptionService>(_ => new EncryptionService(encryptionKey));
builder.Services.AddScoped<IOrganizationRepository, OrganizationRepository>();
builder.Services.AddScoped<IAdoSiteRepository, AdoSiteRepository>();
builder.Services.AddScoped<IPipelineCacheRepository, PipelineCacheRepository>();
builder.Services.AddScoped<IOrganizationService, OrganizationService>();
builder.Services.AddScoped<IAdoSiteService, AdoSiteService>();
builder.Services.AddScoped<IAdoService, AdoService>();
builder.Services.AddScoped<ICompareService, CompareService>();
builder.Services.AddScoped<ICompareExportService, CompareExportService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.Logger.LogInformation("Using SQLite path: {Path}", fullDbPath);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
