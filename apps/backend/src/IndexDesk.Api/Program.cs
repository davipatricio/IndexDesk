using IndexDesk.BuildingBlocks.Cache;
using IndexDesk.BuildingBlocks.Observability;
using IndexDesk.BuildingBlocks.Persistence;
using IndexDesk.Modules.Analytics;
using IndexDesk.Modules.Auth;
using IndexDesk.Modules.MarketData;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Observability (OpenTelemetry -> Jaeger)
builder.Services.AddIndexDeskObservability(builder.Configuration, "IndexDesk.Api");

// Persistence (PostgreSQL / TimescaleDB)
var postgresConnection =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=indexdesk;Username=indexdesk;Password=indexdesk_dev_secret;";

builder.Services.AddDbContext<IndexDeskDbContext>(options =>
{
    options.UseNpgsql(postgresConnection);
});

// Redis Cache
var redisConnection =
    builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379,abortConnect=false";
try
{
    var multiplexer = ConnectionMultiplexer.Connect(redisConnection);
    builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
    builder.Services.AddSingleton<ICacheService, RedisCacheService>();
}
catch
{
    // Fallback in-memory cache if redis is not running during local dev
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSingleton<ICacheService, InMemoryCacheFallback>();
}

// Add Modules
builder.Services.AddAuthModule(builder.Configuration);
builder.Services.AddMarketDataModule(builder.Configuration);
builder.Services.AddAnalyticsModule();

// Health Checks
builder.Services.AddHealthChecks();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowWeb",
        policy =>
        {
            policy
                .WithOrigins(
                    builder.Configuration["Web:AppUrl"] ?? "http://localhost:3000",
                    "http://127.0.0.1:3000"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    );
});

// OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc(
        "v1",
        new OpenApiInfo
        {
            Title = "IndexDesk Financial Intelligence API",
            Version = "v1",
            Description =
                "High-performance modular API for Brazilian B3 ETFs, BDRs, Portfolio Analytics, and Market Data.",
        }
    );

    options.AddSecurityDefinition(
        "Bearer",
        new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter JWT Bearer token.",
        }
    );

    options.AddSecurityRequirement(
        new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer",
                    },
                },
                Array.Empty<string>()
            },
        }
    );
});

var app = builder.Build();

// Configure Middleware Pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "IndexDesk API v1"));
    app.MapScalarApiReference();
}

app.UseCors("AllowWeb");
app.UseAuthentication();
app.UseAuthorization();

// Health Check Endpoint
app.MapHealthChecks("/health");

// Root Endpoint
app.MapGet(
    "/",
    () =>
        Results.Ok(
            new
            {
                Application = "IndexDesk Modular API",
                Status = "Healthy",
                Version = "1.0.0",
                Documentation = "/scalar/v1",
                TimestampUtc = DateTime.UtcNow,
            }
        )
);

// Map Module Endpoints
app.MapAuthEndpoints();
app.MapMarketDataEndpoints();
app.MapAnalyticsEndpoints();

app.Run();

// Needed for WebApplicationFactory in Integration Tests
public partial class Program { }

public sealed class InMemoryCacheFallback : ICacheService
{
    private readonly Dictionary<string, object> _cache = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var val) && val is T typed)
            return Task.FromResult<T?>(typed);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default
    )
    {
        if (value is not null)
            _cache[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken cancellationToken = default
    )
    {
        var existing = await GetAsync<T>(key, cancellationToken);
        if (existing is not null)
            return existing;
        var created = await factory(cancellationToken);
        if (created is not null)
            await SetAsync(key, created, expiration, cancellationToken);
        return created;
    }
}
