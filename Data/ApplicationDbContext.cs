using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

namespace ICP.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RoleTelId> RolesTelId => Set<RoleTelId>();

    public DbSet<RoleDepId> RolesDepId => Set<RoleDepId>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Resource>(entity =>
        {
            entity.ToTable("Resources");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.SystemCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ModuleCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ResourceCode).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ResourceName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.ResourceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Route).HasMaxLength(500);
            entity.Property(e => e.Icon).HasMaxLength(100);
            entity.Property(e => e.Sort).HasDefaultValue(0);
            entity.Property(e => e.IsVisible).HasDefaultValue(true);
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("GETDATE()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);

            entity.HasOne(e => e.Parent)
                .WithMany(e => e.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.RoleCode).HasMaxLength(100).IsRequired();
            entity.Property(e => e.RoleName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("GETDATE()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);

            entity.HasIndex(e => e.RoleCode)
                .IsUnique()
                .HasDatabaseName("IX_Roles_RoleCode");
        });

        modelBuilder.Entity<RoleTelId>(entity =>
        {
            entity.ToTable("RolesTELID");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TelId).HasColumnName("TELID").HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("GETDATE()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);

            entity.HasIndex(e => new { e.TelId, e.RoleId })
                .IsUnique()
                .HasDatabaseName("IX_RolesTELID_TELID_RoleId");

            entity.HasOne(e => e.Role)
                .WithMany(e => e.RoleTelIds)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RoleDepId>(entity =>
        {
            entity.ToTable("RolesDepID");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.DepId).HasColumnName("DepID").HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("GETDATE()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);

            entity.HasIndex(e => new { e.DepId, e.RoleId })
                .IsUnique()
                .HasDatabaseName("IX_RolesDepID_DepID_RoleId");

            entity.HasOne(e => e.Role)
                .WithMany(e => e.RoleDepIds)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ActionCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsAllowed).HasDefaultValue(true);
            entity.Property(e => e.DataScope).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("GETDATE()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);

            entity.HasIndex(e => new { e.RoleId, e.ResourceId, e.ActionCode })
                .IsUnique()
                .HasDatabaseName("IX_RolePermissions");

            entity.HasOne(e => e.Role)
                .WithMany(e => e.RolePermissions)
                .HasForeignKey(e => e.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Resource)
                .WithMany(e => e.RolePermissions)
                .HasForeignKey(e => e.ResourceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
