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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IcpOutboxEntry>(entity =>
        {
            entity.ToTable("INTEGRATION_EVENT_OUTBOX");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.UpdateUser).HasMaxLength(100);
        });
    }
}
