using System.ComponentModel.DataAnnotations;
using EnterpriseAssetLifecycle.Domain;

namespace EnterpriseAssetLifecycle.Contracts;

public sealed record CreateAssetRequest(
    [property: Required, StringLength(40)] string AssetTag,
    AssetType Type,
    [property: Required, StringLength(100)] string Manufacturer,
    [property: Required, StringLength(100)] string Model,
    [property: Required, StringLength(120)] string SerialNumber,
    Guid DepartmentId,
    DateOnly? PurchaseDate);

public sealed record AssignAssetRequest(
    Guid EmployeeId,
    DateTimeOffset? ExpectedReturnAt,
    [property: Range(1, uint.MaxValue)] uint ExpectedVersion,
    [property: Required, StringLength(120)] string Actor);

public sealed record ReturnAssetRequest(
    [property: Range(1, uint.MaxValue)] uint ExpectedVersion,
    [property: Required, StringLength(120)] string Actor,
    [property: StringLength(500)] string? Notes);

public sealed record MoveAssetRequest(
    Guid DepartmentId,
    [property: Range(1, uint.MaxValue)] uint ExpectedVersion,
    [property: Required, StringLength(120)] string Actor);

public sealed record StartRepairRequest(
    [property: Required, StringLength(1000)] string Description,
    [property: StringLength(160)] string? Vendor,
    [property: Range(0, 100000000)] decimal? Cost,
    [property: Range(1, uint.MaxValue)] uint ExpectedVersion,
    [property: Required, StringLength(120)] string Actor);

public sealed record CompleteRepairRequest(
    [property: Range(1, uint.MaxValue)] uint ExpectedVersion,
    [property: Required, StringLength(120)] string Actor);

public sealed record RetireAssetRequest(
    [property: Range(1, uint.MaxValue)] uint ExpectedVersion,
    [property: Required, StringLength(120)] string Actor,
    [property: Required, StringLength(500)] string Reason);

public sealed record UpsertWarrantyRequest(
    [property: Required, StringLength(160)] string Provider,
    DateOnly StartDate,
    DateOnly EndDate,
    [property: StringLength(1000)] string? CoverageNotes,
    [property: Range(1, uint.MaxValue)] uint ExpectedVersion,
    [property: Required, StringLength(120)] string Actor);

public sealed record InstallSoftwareRequest(
    [property: Required, StringLength(160)] string Name,
    [property: Required, StringLength(80)] string Version,
    [property: Range(1, uint.MaxValue)] uint ExpectedVersion,
    [property: Required, StringLength(120)] string Actor);

public sealed record CreateDepartmentRequest(
    [property: Required, StringLength(20)] string Code,
    [property: Required, StringLength(120)] string Name);

public sealed record CreateEmployeeRequest(
    [property: Required, StringLength(40)] string EmployeeNumber,
    [property: Required, StringLength(160)] string FullName,
    [property: Required, EmailAddress, StringLength(254)] string Email,
    Guid DepartmentId);

public sealed record AssetDto(
    Guid Id,
    string AssetTag,
    AssetType Type,
    string Manufacturer,
    string Model,
    string SerialNumber,
    AssetState State,
    Guid DepartmentId,
    string? Department,
    DateOnly? PurchaseDate,
    DateTimeOffset? RetiredAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version,
    AssignmentDto? ActiveAssignment,
    WarrantyDto? Warranty);

public sealed record AssignmentDto(
    Guid Id,
    Guid EmployeeId,
    string? Employee,
    DateTimeOffset AssignedAt,
    DateTimeOffset? ExpectedReturnAt,
    DateTimeOffset? ReturnedAt,
    string AssignedBy);

public sealed record WarrantyDto(
    Guid Id,
    string Provider,
    DateOnly StartDate,
    DateOnly EndDate,
    string? CoverageNotes);

public sealed record AssetEventDto(
    Guid Id,
    AssetEventType Type,
    string Actor,
    DateTimeOffset OccurredAt,
    string Data,
    string? CorrelationId);

public sealed record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record DashboardDto(
    int TotalAssets,
    int InStock,
    int Assigned,
    int InRepair,
    int Retired,
    int ExpiringWarranties,
    int OverdueReturns);

public sealed record ImportResultDto(
    Guid BatchId,
    string IdempotencyKey,
    string Status,
    int TotalRows,
    int ImportedRows,
    int SkippedRows,
    int FailedRows,
    IReadOnlyList<string> Errors,
    bool Replayed);

