using ICP.Models.Ilc;
using Microsoft.EntityFrameworkCore;

namespace ICP.Data;

public class IlcDbContext : DbContext
{
    public IlcDbContext(DbContextOptions<IlcDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserInfoAd> UserInfoAd => Set<UserInfoAd>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserInfoAd>(entity =>
        {
            entity.ToTable("User_Info_AD");
            entity.HasKey(e => e.KeyId);
            entity.Property(e => e.KeyId).HasColumnName("keyID");
            entity.Property(e => e.DepName).HasColumnName("DepName").HasMaxLength(50);
            entity.Property(e => e.UserName).HasColumnName("UserName").HasMaxLength(50);
            entity.Property(e => e.TelId).HasColumnName("TELID").HasMaxLength(50);
            entity.Property(e => e.EmailAddress).HasColumnName("EmailAddress").HasMaxLength(200);
            entity.Property(e => e.DisplayName).HasColumnName("DisplayName").HasMaxLength(100);
            entity.Property(e => e.DepId).HasColumnName("DepID").HasMaxLength(50);
            entity.Property(e => e.DepName2).HasColumnName("DepName2").HasMaxLength(50);
            entity.Property(e => e.CreateDate).HasColumnName("Create_Date").HasMaxLength(50).IsRequired();
        });
    }
}
