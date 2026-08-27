using Microsoft.EntityFrameworkCore;
using TEL.IntegrationHub.Models;

namespace TEL.IntegrationHub.Data;

public sealed class FiestaDbContext(DbContextOptions<FiestaDbContext> options) : DbContext(options)
{
    public DbSet<FiestaMailGroup> MailGroups => Set<FiestaMailGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FiestaMailGroup>(entity =>
        {
            entity.ToTable("MailGroup");
            entity.HasKey(x => x.Uid);
            entity.Property(x => x.Uid).HasColumnName("UID");
            entity.Property(x => x.Address).HasColumnName("Address").HasMaxLength(255);
            entity.Property(x => x.EmpId).HasColumnName("EmpID").HasMaxLength(50);
        });
    }
}
