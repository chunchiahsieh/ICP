using Microsoft.EntityFrameworkCore;
using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Data;

/// <summary>Read/update ICP Outbox only; does not own schema creation.</summary>
public class IcpDbContext : DbContext
{
    public IcpDbContext(DbContextOptions<IcpDbContext> options)
        : base(options)
    {
    }

    public DbSet<IcpOutboxEntry> OutboxEntries => Set<IcpOutboxEntry>();
    public DbSet<IcpSystemConfig> SystemConfigs => Set<IcpSystemConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IcpOutboxEntry>(entity =>
        {
            entity.ToTable("INTEGRATION_EVENT_OUTBOX");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.LastError);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);
        });

        modelBuilder.Entity<IcpSystemConfig>(entity =>
        {
            entity.ToTable("SystemConfigs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasMaxLength(50);
            entity.Property(x => x.Key1).HasMaxLength(100);
            entity.Property(x => x.Value4).HasMaxLength(1000);
        });
    }
}
