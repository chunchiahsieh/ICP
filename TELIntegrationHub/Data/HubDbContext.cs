using Microsoft.EntityFrameworkCore;
using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Data;

public class HubDbContext : DbContext
{
    public HubDbContext(DbContextOptions<HubDbContext> options)
        : base(options)
    {
    }

    public DbSet<MessageLog> MessageLogs => Set<MessageLog>();

    public DbSet<RoutingRule> RoutingRules => Set<RoutingRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageLog>(entity =>
        {
            entity.ToTable("MESSAGE_LOG");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CorrelationId).HasMaxLength(128);
            entity.Property(e => e.EventType).HasMaxLength(128).IsRequired();
            entity.Property(e => e.SourceSystem).HasMaxLength(64).IsRequired();
            entity.Property(e => e.TargetSystem).HasMaxLength(64);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => new { e.SourceSystem, e.EventType, e.Status });
        });

        modelBuilder.Entity<RoutingRule>(entity =>
        {
            entity.ToTable("ROUTING_RULE");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SourceSystem).HasMaxLength(64).IsRequired();
            entity.Property(e => e.EventType).HasMaxLength(128).IsRequired();
            entity.Property(e => e.TargetSystem).HasMaxLength(64).IsRequired();
            entity.Property(e => e.TargetType).HasMaxLength(32).IsRequired();
            entity.Property(e => e.TargetName).HasMaxLength(128).IsRequired();
        });
    }
}
