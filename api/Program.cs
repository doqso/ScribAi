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
using ScribAi.Api.Services;
using ScribAi.Api.Storage;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

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

builder.Services.AddHttpClient<IOllamaExtractor, OllamaExtractor>((sp, c) =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
    c.BaseAddress = new Uri(opt.BaseUrl);
    c.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
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

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:4200"])
     .AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseCors();

app.MapOpenApi();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapHealth();
app.MapBootstrap();
app.MapExtractions();
app.MapSchemas();
app.MapWebhooks();
app.MapKeys();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScribaiDbContext>();
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Initial DB migration failed — will retry on first request");
    }

    try
    {
        var blob = scope.ServiceProvider.GetRequiredService<IBlobStore>();
        await blob.EnsureBucketAsync(CancellationToken.None);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Bucket init failed");
    }
}

app.Run();

public partial class Program { }
