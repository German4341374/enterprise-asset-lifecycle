using EnterpriseAssetLifecycle.Domain;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Infrastructure;

public sealed class AssetDbContext(DbContextOptions<AssetDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<Maintenance> MaintenanceRecords => Set<Maintenance>();
    public DbSet<Warranty> Warranties => Set<Warranty>();
    public DbSet<SoftwareInstallation> SoftwareInstallations => Set<SoftwareInstallation>();
    public DbSet<AssetEvent> AssetEvents => Set<AssetEvent>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresEnum<AssetState>();
        modelBuilder.HasPostgresEnum<AssetType>();
        modelBuilder.HasPostgresEnum<MaintenanceStatus>();
        modelBuilder.HasPostgresEnum<AssetEventType>();
        modelBuilder.HasPostgresEnum<ImportStatus>();

        modelBuilder.Entity<Department>(entity =>
        {
            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasIndex(x => x.EmployeeNumber).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasIndex(x => new { x.DepartmentId, x.IsActive });
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasIndex(x => x.AssetTag).IsUnique();
            entity.HasIndex(x => x.SerialNumber).IsUnique();
            entity.HasIndex(x => x.State);
            entity.HasIndex(x => new { x.DepartmentId, x.State });
            entity.HasIndex(x => x.UpdatedAt);
            entity.Property(x => x.Version).IsRowVersion().HasColumnName("xmin");
        });

        modelBuilder.Entity<Assignment>(entity =>
        {
            entity.HasIndex(x => new { x.AssetId, x.AssignedAt });
            entity.HasIndex(x => x.EmployeeId);
            entity.HasIndex(x => x.AssetId)
                .HasFilter("\"ReturnedAt\" IS NULL")
                .IsUnique();
        });

        modelBuilder.Entity<Maintenance>(entity =>
        {
            entity.HasIndex(x => new { x.AssetId, x.Status });
            entity.Property(x => x.Cost).HasPrecision(12, 2);
        });

        modelBuilder.Entity<Warranty>(entity =>
        {
            entity.HasIndex(x => x.AssetId).IsUnique();
            entity.HasIndex(x => x.EndDate);
        });

        modelBuilder.Entity<SoftwareInstallation>(entity =>
        {
            entity.HasIndex(x => new { x.AssetId, x.Name });
        });

        modelBuilder.Entity<AssetEvent>(entity =>
        {
            entity.HasIndex(x => new { x.AssetId, x.OccurredAt });
            entity.HasIndex(x => x.DeduplicationKey).IsUnique();
            entity.Property(x => x.Data).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ImportBatch>(entity =>
        {
            entity.HasIndex(x => x.IdempotencyKey).IsUnique();
            entity.HasIndex(x => x.FileHash);
            entity.Property(x => x.Errors).HasColumnType("jsonb");
        });
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ProtectAppendOnlyEvents();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ProtectAppendOnlyEvents();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ProtectAppendOnlyEvents()
    {
        var changedEvents = ChangeTracker.Entries<AssetEvent>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);

        if (changedEvents)
        {
            throw new InvalidOperationException("Asset events are append-only and cannot be modified or deleted.");
        }
    }
}

