using Microsoft.EntityFrameworkCore;
using ScribAi.Api.Data.Entities;

namespace ScribAi.Api.Data;

public class ScribaiDbContext(DbContextOptions<ScribaiDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<SchemaDefinition> Schemas => Set<SchemaDefinition>();
    public DbSet<Extraction> Extractions => Set<Extraction>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<TenantSettings> TenantSettings => Set<TenantSettings>();
    public DbSet<GlobalSettings> GlobalSettings => Set<GlobalSettings>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        });

        b.Entity<ApiKey>(e =>
        {
            e.ToTable("api_keys");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.Property(x => x.KeyHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.KeyPrefix).HasMaxLength(16).IsRequired();
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.DefaultModel).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Tenant).WithMany(t => t.ApiKeys).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SchemaDefinition>(e =>
        {
            e.ToTable("schemas");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ApiKeyId, x.Name, x.Version }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.JsonSchema).HasColumnType("jsonb").IsRequired();
            e.HasOne(x => x.Tenant).WithMany(t => t.Schemas).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Extraction>(e =>
        {
            e.ToTable("extractions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.SourceFilename).HasMaxLength(500).IsRequired();
            e.Property(x => x.Mime).HasMaxLength(200).IsRequired();
            e.Property(x => x.Model).HasMaxLength(200);
            e.Property(x => x.ExtractionMethod).HasMaxLength(50);
            e.Property(x => x.JsonSchemaSnapshot).HasColumnType("jsonb");
            e.Property(x => x.Result).HasColumnType("jsonb");
            e.Property(x => x.WebhookUrl).HasMaxLength(2000);
            e.HasOne(x => x.Tenant).WithMany(t => t.Extractions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Webhook>(e =>
        {
            e.ToTable("webhooks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Url).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Secret).HasMaxLength(200).IsRequired();
            e.Property(x => x.Events).HasColumnType("text[]");
            e.HasOne(x => x.Tenant).WithMany(t => t.Webhooks).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<WebhookDelivery>(e =>
        {
            e.ToTable("webhook_deliveries");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.WebhookId, x.CreatedAt });
            e.Property(x => x.Event).HasMaxLength(100);
            e.Property(x => x.Response).HasColumnType("text");
        });

        b.Entity<TenantSettings>(e =>
        {
            e.ToTable("tenant_settings");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.DefaultTextModel).HasMaxLength(200);
            e.Property(x => x.VisionModel).HasMaxLength(200);
            e.HasOne(x => x.Tenant).WithOne().HasForeignKey<TenantSettings>(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GlobalSettings>(e =>
        {
            e.ToTable("global_settings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.ToTable(t => t.HasCheckConstraint("ck_global_settings_singleton", "\"Id\" = 1"));
            e.Property(x => x.SeqUrl).HasMaxLength(500);
            e.Property(x => x.SeqMinimumLevel).HasMaxLength(20);
            e.Property(x => x.ApplicationName).HasMaxLength(100).IsRequired();
            e.Property(x => x.AllowedOrigins).HasColumnType("text[]");
        });

        b.Entity<AuditEvent>(e =>
        {
            e.ToTable("audit_events");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.HasIndex(x => x.EventType);
            e.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Target).HasMaxLength(500);
            e.Property(x => x.Details).HasColumnType("jsonb");
        });
    }
}
