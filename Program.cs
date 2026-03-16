using DotNetEnv;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using pqy_server.Data;
using pqy_server.Extensions;
using pqy_server.Middlewares;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;

// Load environment variables (.env for local, system env for production)
Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Core infrastructure
builder.Services.AddAppRateLimiting();
builder.Services.AddAppDatabase();
builder.Services.AddAppAuthentication(builder.Configuration);
builder.Services.AddAppCors(builder.Configuration);
builder.Services.AddAppCompression();
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("LookupPolicy", builder => 
        builder.Expire(TimeSpan.FromHours(1)).Tag("lookup"));
});
builder.Services.AddMemoryCache();
builder.Services.AddApplicationServices(builder.Configuration);

// Critical #1 fix: configure forwarded headers safely so Railway's proxy
// sets RemoteIpAddress correctly. RateLimiter reads RemoteIpAddress
// instead of blindly trusting the X-Forwarded-For header.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Trust only the single Railway reverse-proxy hop. ForwardLimit=1 ensures only
    // the rightmost X-Forwarded-For entry is used, preventing IP spoofing by clients
    // who inject extra entries. KnownNetworks/KnownProxies are cleared because
    // Railway's egress IPs are not static, but ForwardLimit=1 is the real guard.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});

// Real-time admin push
builder.Services.AddSignalR();

// Controllers & JSON settings
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles);

// API documentation (dev only)
builder.Services.AddOpenApi();

// Structured logging (Serilog)
builder.AddAppSerilog();

// EPPlus license
ExcelPackage.License.SetNonCommercialPersonal("InstaPYQ");

var app = builder.Build();

// Auto-apply pending EF migrations on startup (safe: idempotent, runs before requests)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// External initializations
app.UseFirebase();

// HTTP pipeline
app.UseAppPipeline();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Apply DB migrations & seed
app.ApplyMigrations();

// Port binding (Railway sets PORT automatically)
if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PORT")))
{
    // Only set default port if not running on Railway
    var port = "5000";
    if (app.Urls.Count == 0)
    {
        app.Urls.Add($"http://0.0.0.0:{port}");
    }
}

app.Run();

