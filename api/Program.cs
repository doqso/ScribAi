using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Auth;
using ScribAi.Api.Data;
using ScribAi.Api.Endpoints;
using ScribAi.Api.Jobs;
using ScribAi.Api.Options;
using ScribAi.Api.Pipeline;
using ScribAi.Api.Pipeline.Extractors;
using ScribAi.Api.Pipeline.Llm;
using ScribAi.Api.Pipeline.Ocr;
using ScribAi.Api.Security;
using ScribAi.Api.Services;
using ScribAi.Api.Storage;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Serilog bootstrap (no DI during config — sink and enricher are static singletons)
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With(new ScribAi.Api.Logging.TenantEnricher())
    .WriteTo.Console()
    .WriteTo.Sink(ScribAi.Api.Logging.DynamicSeqSinkHolder.Instance));

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection(OllamaOptions.Section));
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
builder.Services.Configure<ProcessingOptions>(builder.Configuration.GetSection(ProcessingOptions.Section));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.Section));

var pg = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required");

builder.Services.AddDbContextFactory<ScribaiDbContext>(o => o.UseNpgsql(pg));
builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<ScribaiDbContext>>().CreateDbContext());

var redisConn = builder.Configuration.GetSection(RedisOptions.Section).Get<RedisOptions>()?.ConnectionString
    ?? "redis:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

builder.Services.AddSingleton<IBlobStore, MinioBlobStore>();
builder.Services.AddSingleton<ITesseractOcr, TesseractOcr>();

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ISecretsProtector, SecretsProtector>();
builder.Services.AddSingleton<IGlobalSettingsProvider, GlobalSettingsProvider>();
builder.Services.AddSingleton<ITenantSettingsService, TenantSettingsService>();
builder.Services.AddSingleton<IAuditLogger, AuditLogger>();

builder.Services.AddHttpClient<IOllamaExtractor, OllamaExtractor>(c =>
{
    c.Timeout = TimeSpan.FromMinutes(30);
});

builder.Services.AddHttpClient("ollama-meta", c =>
{
    c.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<WebhookDispatcher>();

builder.Services.AddScoped<IDocumentRouter, DocumentRouter>();
builder.Services.AddScoped<PdfExtractor>();
builder.Services.AddScoped<ImageExtractor>();
builder.Services.AddScoped<OfficeExtractor>();
builder.Services.AddScoped<EmailExtractor>();
builder.Services.AddScoped<PlainTextExtractor>();
builder.Services.AddScoped<ExtractionService>();

builder.Services.AddSingleton<IJobQueue, RedisJobQueue>();
builder.Services.AddHostedService<ExtractionWorker>();

builder.Services.AddSingleton<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider, ScribAi.Api.Cors.DynamicCorsPolicyProvider>();
builder.Services.AddCors();

builder.Services.AddOpenApi();

var app = builder.Build();

ScribAi.Api.Logging.TenantEnricher.UseAccessor(app.Services.GetRequiredService<IHttpContextAccessor>());
ScribAi.Api.Logging.DynamicSeqSinkHolder.Instance.Attach(app.Services.GetRequiredService<IGlobalSettingsProvider>());

app.UseSerilogRequestLogging();
app.UseCors(p => { /* dynamic provider reads current GlobalSettings per request */ });

app.MapOpenApi();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapHealth();
app.MapBootstrap();
app.MapMe();
app.MapExtractions();
app.MapSchemas();
app.MapWebhooks();
app.MapKeys();
app.MapSettings();
app.MapAdminGlobal();
app.MapAudit();
app.MapExports();
app.MapStreaming();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScribaiDbContext>();
    try { await db.Database.MigrateAsync(); }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Initial DB migration failed"); }

    try
    {
        var blob = scope.ServiceProvider.GetRequiredService<IBlobStore>();
        await blob.EnsureBucketAsync(CancellationToken.None);
    }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Bucket init failed"); }

    try
    {
        var gs = scope.ServiceProvider.GetRequiredService<IGlobalSettingsProvider>();
        await gs.ReloadAsync();
    }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Global settings initial load failed"); }
}

app.Run();

public partial class Program { }
