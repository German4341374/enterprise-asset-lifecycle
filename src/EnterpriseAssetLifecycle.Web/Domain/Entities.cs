using System.ComponentModel.DataAnnotations;

namespace EnterpriseAssetLifecycle.Domain;

public sealed class Asset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(40)] public required string AssetTag { get; set; }
    public AssetType Type { get; set; }
    [MaxLength(100)] public required string Manufacturer { get; set; }
    [MaxLength(100)] public required string Model { get; set; }
    [MaxLength(120)] public required string SerialNumber { get; set; }
    public AssetState State { get; set; } = AssetState.InStock;
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public uint Version { get; set; }
    public List<Assignment> Assignments { get; set; } = [];
    public List<Maintenance> MaintenanceRecords { get; set; } = [];
    public List<SoftwareInstallation> SoftwareInstallations { get; set; } = [];
    public List<AssetEvent> Events { get; set; } = [];
    public Warranty? Warranty { get; set; }
}

public sealed class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(40)] public required string EmployeeNumber { get; set; }
    [MaxLength(160)] public required string FullName { get; set; }
    [MaxLength(254)] public required string Email { get; set; }
    public Guid DepartmentId { get; set; }
    public Department? Department { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Assignment> Assignments { get; set; } = [];
}

public sealed class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(20)] public required string Code { get; set; }
    [MaxLength(120)] public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Asset> Assets { get; set; } = [];
    public List<Employee> Employees { get; set; } = [];
}

public sealed class Assignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    public Guid EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpectedReturnAt { get; set; }
    public DateTimeOffset? ReturnedAt { get; set; }
    [MaxLength(120)] public required string AssignedBy { get; set; }
    [MaxLength(500)] public string? ReturnNotes { get; set; }
}

public sealed class Maintenance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    [MaxLength(1000)] public required string Description { get; set; }
    [MaxLength(160)] public string? Vendor { get; set; }
    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Open;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    [Range(0, 100000000)] public decimal? Cost { get; set; }
}

public sealed class Warranty
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    [MaxLength(160)] public required string Provider { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    [MaxLength(1000)] public string? CoverageNotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class SoftwareInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    [MaxLength(160)] public required string Name { get; set; }
    [MaxLength(80)] public required string Version { get; set; }
    public DateTimeOffset InstalledAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UninstalledAt { get; set; }
}

public sealed class AssetEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AssetId { get; set; }
    public Asset? Asset { get; set; }
    public AssetEventType Type { get; set; }
    [MaxLength(120)] public required string Actor { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string Data { get; set; } = "{}";
    [MaxLength(100)] public string? CorrelationId { get; set; }
    [MaxLength(180)] public string? DeduplicationKey { get; set; }
}

public sealed class ImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(120)] public required string IdempotencyKey { get; set; }
    [MaxLength(64)] public required string FileHash { get; set; }
    public ImportStatus Status { get; set; } = ImportStatus.Processing;
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int SkippedRows { get; set; }
    public int FailedRows { get; set; }
    public string Errors { get; set; } = "[]";
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}

