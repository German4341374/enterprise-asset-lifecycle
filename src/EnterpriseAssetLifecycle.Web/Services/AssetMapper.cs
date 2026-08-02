using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Domain;

namespace EnterpriseAssetLifecycle.Services;

public static class AssetMapper
{
    public static AssetDto ToDto(Asset asset)
    {
        var active = asset.Assignments.FirstOrDefault(x => x.ReturnedAt is null);
        return new AssetDto(
            asset.Id,
            asset.AssetTag,
            asset.Type,
            asset.Manufacturer,
            asset.Model,
            asset.SerialNumber,
            asset.State,
            asset.DepartmentId,
            asset.Department?.Name,
            asset.PurchaseDate,
            asset.RetiredAt,
            asset.CreatedAt,
            asset.UpdatedAt,
            asset.Version,
            active is null
                ? null
                : new AssignmentDto(
                    active.Id,
                    active.EmployeeId,
                    active.Employee?.FullName,
                    active.AssignedAt,
                    active.ExpectedReturnAt,
                    active.ReturnedAt,
                    active.AssignedBy),
            asset.Warranty is null
                ? null
                : new WarrantyDto(
                    asset.Warranty.Id,
                    asset.Warranty.Provider,
                    asset.Warranty.StartDate,
                    asset.Warranty.EndDate,
                    asset.Warranty.CoverageNotes));
    }
}

