using ICP.Models.Fiesta;
using Microsoft.EntityFrameworkCore;

namespace ICP.Data;

public class FiestaDbContext : DbContext
{
    public FiestaDbContext(DbContextOptions<FiestaDbContext> options)
        : base(options)
    {
    }

    public DbSet<MailGroup> MailGroup => Set<MailGroup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MailGroup>(entity =>
        {
            entity.ToTable("MailGroup");
            entity.HasKey(e => e.Uid);
            entity.Property(e => e.Uid).HasColumnName("UID");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255);
            entity.Property(e => e.Address).HasColumnName("Address").HasMaxLength(255);
            entity.Property(e => e.EmpId).HasColumnName("EmpID").HasMaxLength(50);
        });
    }
}
