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

    public DbSet<RoleMailGroup> RolesMailGroup => Set<RoleMailGroup>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    public DbSet<ForwarderDataUpload> ForwarderDataUploads => Set<ForwarderDataUpload>();

    public DbSet<TariffData> TariffDataRecords => Set<TariffData>();

    public DbSet<IcpHeader> IcpHeaders => Set<IcpHeader>();

    public DbSet<IcpDetail> IcpDetails => Set<IcpDetail>();

    public DbSet<ShipInfoAuditLog> ShipInfoAuditLogs => Set<ShipInfoAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.ToTable("SystemConfigs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FunctionCode).HasMaxLength(50);
            entity.Property(e => e.Key1).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Key2).HasMaxLength(100).HasDefaultValue(string.Empty);
            entity.Property(e => e.Value1).HasMaxLength(1000);
            entity.Property(e => e.Value2).HasMaxLength(1000);
            entity.Property(e => e.Value3).HasMaxLength(1000);
            entity.Property(e => e.Value4).HasMaxLength(1000);
            entity.Property(e => e.Value5).HasMaxLength(1000);
            entity.Property(e => e.Value6).HasMaxLength(1000);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("sysdatetime()");
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);

            entity.HasIndex(e => new { e.Category, e.FunctionCode, e.Key1, e.Key2 })
                .IsUnique()
                .HasDatabaseName("UX_SystemConfigs")
                .HasFilter("[IsDeleted] = 0");
        });

        modelBuilder.Entity<ForwarderDataUpload>(entity =>
        {
            entity.ToTable("ForwarderDataUpload");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Type).HasMaxLength(20).IsRequired();
            entity.Property(e => e.InvoiceNo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CustomerReference).HasMaxLength(100);
            entity.Property(e => e.MaterialCode).HasMaxLength(100);
            entity.Property(e => e.OrderMaterialName).HasMaxLength(500);
            entity.Property(e => e.Quantity).HasPrecision(18, 4);
            entity.Property(e => e.PortOfLoading).HasMaxLength(100);
            entity.Property(e => e.ShipToName).HasMaxLength(300);
            entity.Property(e => e.ShipToAddress).HasColumnType("nvarchar(max)");
            entity.Property(e => e.ShipToPartyCountryCode).HasMaxLength(100);
            entity.Property(e => e.ShipToPortCode).HasMaxLength(50);
            entity.Property(e => e.FreightCharge).HasMaxLength(100);
            entity.Property(e => e.Hawb).HasMaxLength(50);
            entity.Property(e => e.Mawb).HasMaxLength(50);
            entity.Property(e => e.Flight1).HasMaxLength(50);
            entity.Property(e => e.Flight2).HasMaxLength(50);
            entity.Property(e => e.Cb).HasMaxLength(50);
            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.Mdp).HasMaxLength(50);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("getdate()");
            entity.Property(e => e.CreateUser).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UpdateUser).HasMaxLength(50);
            entity.Property(e => e.FilePath).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<TariffData>(entity =>
        {
            entity.ToTable("TariffData");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.MAWB).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HAWB).HasMaxLength(50).IsRequired();
            entity.Property(e => e.LineNo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PartNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.InvoiceNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PONumber).HasMaxLength(100);
            entity.Property(e => e.DescriptionOfGoods).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Quantity).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UOM).HasMaxLength(50).IsRequired();
            entity.Property(e => e.NetWeightKg).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UnitValue).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HTSNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.COO).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DutyRate).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DutyTreatment).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PermitNo1).HasMaxLength(100);
            entity.Property(e => e.PermitItem1).HasMaxLength(100);
            entity.Property(e => e.PermitNo2).HasMaxLength(100);
            entity.Property(e => e.PermitItem2).HasMaxLength(100);
            entity.Property(e => e.PermitNo3).HasMaxLength(100);
            entity.Property(e => e.PermitItem3).HasMaxLength(100);
            entity.Property(e => e.EntryNumber).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Mode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PortOfDeparture).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FlightNo).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Shipper).HasMaxLength(200);
            entity.Property(e => e.TermsOfTrade).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExchangeRate).HasMaxLength(50).IsRequired();
            entity.Property(e => e.CIFValue).HasMaxLength(50);
            entity.Property(e => e.FreightCharge).HasMaxLength(50);
            entity.Property(e => e.TotalPieces).HasMaxLength(50);
            entity.Property(e => e.GrossWeightKg).HasMaxLength(50);
            entity.Property(e => e.Broker).HasMaxLength(200);
            entity.Property(e => e.AirSea).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TotalAmountForeignCurrency).HasPrecision(18, 4);
            entity.Property(e => e.TotalAmountTWD).HasPrecision(18, 4);
            entity.Property(e => e.DeclarationAmountTWD).HasMaxLength(50).IsRequired();
            entity.Property(e => e.DeclarationFile).HasMaxLength(500);
            entity.Property(e => e.Cost).HasMaxLength(50);
            entity.Property(e => e.ImportFileName).HasMaxLength(255);
            entity.Property(e => e.CreateUser).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UpdateUser).HasMaxLength(50);

            entity.HasIndex(e => e.InvoiceNumber)
                .IsUnique()
                .HasDatabaseName("UQ_TariffData_InvoiceNumber");
        });

        modelBuilder.Entity<IcpHeader>(entity =>
        {
            entity.ToTable("ICP_HEADER");
            entity.HasKey(e => e.Id);
            entity.HasAlternateKey(e => new { e.InvoiceNo, e.TetPo });
            entity.Property(e => e.Id).HasDefaultValueSql("newid()");
            entity.Property(e => e.InvoiceNo).HasMaxLength(30).IsRequired();
            entity.Property(e => e.TetPo).HasMaxLength(35).IsRequired();
            entity.Property(e => e.CreateDate).HasMaxLength(20);
            entity.Property(e => e.Status).HasMaxLength(200);
            entity.Property(e => e.SaDate).HasMaxLength(10);
            entity.Property(e => e.Forwarder).HasMaxLength(50);
            entity.Property(e => e.Broker).HasMaxLength(30);
            entity.Property(e => e.Etd).HasMaxLength(10);
            entity.Property(e => e.Eta).HasMaxLength(10);
            entity.Property(e => e.InvoiceDate).HasMaxLength(10);
            entity.Property(e => e.Mawb).HasMaxLength(20);
            entity.Property(e => e.Hawb).HasMaxLength(20);
            entity.Property(e => e.Flt).HasMaxLength(20);
            entity.Property(e => e.Freight).HasMaxLength(10);
            entity.Property(e => e.DestinationPort).HasMaxLength(10);
            entity.Property(e => e.DestinationCountry).HasMaxLength(3);
            entity.Property(e => e.Warehouse).HasMaxLength(20);
            entity.Property(e => e.InvoiceType).HasMaxLength(10);
            entity.Property(e => e.Incoterms).HasMaxLength(20);
            entity.Property(e => e.OrderType).HasMaxLength(20);
            entity.Property(e => e.DeliveryDate).HasMaxLength(10);
            entity.Property(e => e.DeliveryTo).HasMaxLength(20);
            entity.Property(e => e.Bu).HasMaxLength(40);
            entity.Property(e => e.MdpFlag).HasMaxLength(5);
            entity.Property(e => e.NcdrNo).HasMaxLength(60);
            entity.Property(e => e.NcdrRequestor).HasMaxLength(40);
            entity.Property(e => e.EndUserCode).HasMaxLength(30);
            entity.Property(e => e.EndUser).HasMaxLength(100);
            entity.Property(e => e.RtNo).HasMaxLength(30);
            entity.Property(e => e.Receiver).HasMaxLength(200);
            entity.Property(e => e.Owner).HasMaxLength(50);
            entity.Property(e => e.MachineNo).HasMaxLength(50);
            entity.Property(e => e.MachineType).HasMaxLength(50);
            entity.Property(e => e.ShipReason).HasMaxLength(50);
            entity.Property(e => e.Forklift).HasMaxLength(50);
            entity.Property(e => e.MovingLabor).HasMaxLength(50);
            entity.Property(e => e.CarMethod).HasMaxLength(50);
            entity.Property(e => e.ArriveTime).HasMaxLength(50);
            entity.Property(e => e.WasteDisposal).HasMaxLength(50);
            entity.Property(e => e.DriverDetails).HasMaxLength(50);
            entity.Property(e => e.OrderReason).HasMaxLength(50);
            entity.Property(e => e.ArrivalNoticeFlag).HasMaxLength(5);
            entity.Property(e => e.ArrivalNotice).HasMaxLength(100);
            entity.Property(e => e.ReasonForDeliveryDelay).HasMaxLength(200);
            entity.Property(e => e.DelayNotificationDate).HasMaxLength(10);
            entity.Property(e => e.DeliveryNo).HasMaxLength(30);
            entity.Property(e => e.SoldToPartyCode).HasMaxLength(30);
            entity.Property(e => e.SoldToParty).HasMaxLength(100);
            entity.Property(e => e.ShipToPartyCode).HasMaxLength(30);
            entity.Property(e => e.ShipToParty).HasMaxLength(100);
            entity.Property(e => e.ShipToPartyAddress).HasMaxLength(200);
            entity.Property(e => e.EmgFlight).HasMaxLength(5);
            entity.Property(e => e.WbsElement).HasMaxLength(30);
            entity.Property(e => e.Deposit).HasMaxLength(10);
            entity.Property(e => e.SapRemarks).HasMaxLength(1000);
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.Cancellation).HasMaxLength(10);
            entity.Property(e => e.ReasonForCancellation).HasMaxLength(200);
            entity.Property(e => e.AttachedFile).HasMaxLength(1000);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("getdate()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);
            entity.HasMany(e => e.Details)
                .WithOne(d => d.Header)
                .HasForeignKey(d => new { d.InvoiceNo, d.TetPo })
                .HasPrincipalKey(h => new { h.InvoiceNo, h.TetPo })
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IcpDetail>(entity =>
        {
            entity.ToTable("ICP_DETAIL");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("newid()");
            entity.Property(e => e.InvoiceNo).HasMaxLength(30).IsRequired();
            entity.Property(e => e.TetPo).HasMaxLength(35).IsRequired();
            entity.Property(e => e.TetPoLine).HasMaxLength(35);
            entity.Property(e => e.ItemNo).HasMaxLength(47);
            entity.Property(e => e.Description).HasMaxLength(60);
            entity.Property(e => e.Qty).HasColumnType("numeric(13, 3)");
            entity.Property(e => e.Uom).HasMaxLength(10);
            entity.Property(e => e.Coo).HasMaxLength(50);
            entity.Property(e => e.Currency).HasMaxLength(3);
            entity.Property(e => e.Rate).HasColumnType("numeric(18, 4)");
            entity.Property(e => e.PackingType).HasMaxLength(50);
            entity.Property(e => e.GrossWeight).HasColumnType("numeric(6, 3)");
            entity.Property(e => e.Eccn).HasMaxLength(10);
            entity.Property(e => e.ElFlag).HasMaxLength(5);
            entity.Property(e => e.SdsFlag).HasMaxLength(5);
            entity.Property(e => e.Hazmat).HasMaxLength(5);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("getdate()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);
        });

        modelBuilder.Entity<ShipInfoAuditLog>(entity =>
        {
            entity.ToTable("SHIPINFO_AUDIT_LOG");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EntityType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.EntityKey).HasMaxLength(200).IsRequired();
            entity.Property(e => e.HeaderKey).HasMaxLength(200);
            entity.Property(e => e.Action).HasMaxLength(20).IsRequired();
            entity.Property(e => e.FieldName).HasMaxLength(100);
            entity.Property(e => e.UserName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.CaseType).HasMaxLength(20);
            entity.Property(e => e.CaseNo).HasMaxLength(50);
            entity.Property(e => e.OldStatus).HasMaxLength(50);
            entity.Property(e => e.NewStatus).HasMaxLength(50);
            entity.Property(e => e.ActionTime).HasColumnType("datetime2(7)").IsRequired();
            entity.Property(e => e.CreateTime).HasDefaultValueSql("getdate()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);
        });

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

        modelBuilder.Entity<RoleMailGroup>(entity =>
        {
            entity.ToTable("RolesMailGroup");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Address).HasColumnName("Address").HasMaxLength(255).IsRequired();
            entity.Property(e => e.IsEnabled).HasDefaultValue(true);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.CreateTime).HasDefaultValueSql("GETDATE()").IsRequired();
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);

            entity.HasIndex(e => new { e.Address, e.RoleId })
                .IsUnique()
                .HasDatabaseName("IX_RolesMailGroup_Address_RoleId");

            entity.HasOne(e => e.Role)
                .WithMany(e => e.RoleMailGroups)
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

        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.ToTable("SystemConfigs");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Category).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FunctionCode).HasMaxLength(50);
            entity.Property(e => e.Key1).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Key2).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Value1).HasMaxLength(1000);
            entity.Property(e => e.Value2).HasMaxLength(1000);
            entity.Property(e => e.Value3).HasMaxLength(1000);
            entity.Property(e => e.Value4).HasMaxLength(1000);
            entity.Property(e => e.Value5).HasMaxLength(1000);
            entity.Property(e => e.Value6).HasMaxLength(1000);
            entity.Property(e => e.CreateUser).HasMaxLength(100);
            entity.Property(e => e.UpdateUser).HasMaxLength(100);

            entity.HasIndex(e => new { e.Category, e.FunctionCode, e.Key1, e.Key2 })
                .IsUnique()
                .HasDatabaseName("UX_SystemConfigs")
                .HasFilter("[IsDeleted] = 0");
        });
    }
}
