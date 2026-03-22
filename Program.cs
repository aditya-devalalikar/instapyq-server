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

// Reduce thread pool minimum threads from the default (= CPU core count) to 2.
// Each idle thread in the pool holds a ~256 KB stack on the OS.
// On a 2-vCPU Railway container the default min is 2 anyway, but on larger
// plans it could be 4–8. Setting it explicitly ensures predictable behaviour.
// Min of 2 is enough: one thread handles requests, one handles background work
// (StreakAlertHostedService, GC finalizer). The pool grows beyond min on demand.
ThreadPool.SetMinThreads(2, 2);

var builder = WebApplication.CreateBuilder(args);

// Kestrel tuning for a low-traffic Railway container.
// Defaults are designed for high-throughput servers and keep connections +
// memory alive far longer than needed here.
builder.WebHost.ConfigureKestrel(options =>
{
    // Close idle keep-alive connections after 2 minutes instead of the
    // default 130 s with no upper bound — frees sockets + associated buffers.
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    // Abort requests that don't send headers within 30 s (default is 30 s,
    // explicitly set here for clarity and future-proofing).
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    // 500 simultaneous open connections — enough headroom for ~50 concurrent
    // active users (each typically holds 2–4 connections via keep-alive + assets)
    // while still capping runaway spikes that would exhaust thread-pool + memory.
    options.Limits.MaxConcurrentConnections = 500;
    // SignalR is only used for the admin hub — 50 is generous.
    options.Limits.MaxConcurrentUpgradedConnections = 50;
});

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
    // Cap each individual cached HTTP response at 512 KB.
    // Without this a single large response (e.g. a big lookup payload) can
    // consume tens of MB in the output-cache store.
    options.MaximumBodySize = 512 * 1024;
    // Cap total output-cache store at 32 MB.
    // Default is unbounded — Railway containers would accumulate this silently.
    options.SizeLimit = 32 * 1024 * 1024;
});
// SizeLimit caps total cache entries at 1000 units (each entry counts as 1 unit
// via SetSize(1) at every cache.Set / GetOrCreateAsync call site).
// 1000 comfortably covers ~400 active users worth of per-user entries
// (premium status, selected exam IDs, exam candidates) plus shared entries
// (leaderboard snapshots, enum labels, progress summaries) with room to spare.
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000;
});
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

// Real-time admin push — reduce buffer sizes since the admin hub only
// sends small JSON payloads. Default max message size is 32 KB which
// is 32× what we need; 4 KB comfortably fits any admin broadcast.
builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 4 * 1024; // 4 KB
});

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

// External initializations
app.UseFirebase();

// HTTP pipeline
app.UseAppPipeline();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// Apply pending EF migrations and seed on startup
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

