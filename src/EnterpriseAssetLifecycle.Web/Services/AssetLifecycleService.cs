using System.Data;
using System.Text.Json;
using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Domain;
using EnterpriseAssetLifecycle.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Services;

public sealed class AssetLifecycleService(AssetDbContext db, TimeProvider timeProvider)
{
    public async Task<AssetDto> RegisterAsync(
        CreateAssetRequest request,
        string actor,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        _ = await db.Departments.FindAsync([request.DepartmentId], cancellationToken)
            ?? throw new ResourceNotFoundException(nameof(Department), request.DepartmentId);

        var now = timeProvider.GetUtcNow();
        var asset = new Asset
        {
            AssetTag = request.AssetTag.Trim(),
            Type = request.Type,
            Manufacturer = request.Manufacturer.Trim(),
            Model = request.Model.Trim(),
            SerialNumber = request.SerialNumber.Trim(),
            DepartmentId = request.DepartmentId,
            PurchaseDate = request.PurchaseDate,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.Registered, actor, correlationId, new
        {
            asset.AssetTag,
            asset.SerialNumber,
            asset.DepartmentId
        }));

        db.Assets.Add(asset);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(asset.Id, cancellationToken);
    }

    public async Task<AssetDto> AssignAsync(
        Guid assetId,
        AssignAssetRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var asset = await FindForUpdateAsync(assetId, cancellationToken);
        SetExpectedVersion(asset, request.ExpectedVersion);
        AssetStateMachine.EnsureTransition(asset.State, AssetState.Assigned);

        var employee = await db.Employees.SingleOrDefaultAsync(
            x => x.Id == request.EmployeeId,
            cancellationToken) ?? throw new ResourceNotFoundException(nameof(Employee), request.EmployeeId);

        if (!employee.IsActive)
        {
            throw new DomainRuleException("EMPLOYEE_INACTIVE", "Equipment cannot be assigned to an inactive employee.");
        }

        var now = timeProvider.GetUtcNow();
        asset.State = AssetState.Assigned;
        asset.DepartmentId = employee.DepartmentId;
        asset.UpdatedAt = now;
        db.Assignments.Add(new Assignment
        {
            AssetId = asset.Id,
            EmployeeId = employee.Id,
            AssignedAt = now,
            ExpectedReturnAt = request.ExpectedReturnAt,
            AssignedBy = request.Actor.Trim()
        });
        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.Assigned, request.Actor, correlationId, new
        {
            employee.Id,
            employee.EmployeeNumber,
            request.ExpectedReturnAt
        }));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception is not DbUpdateConcurrencyException)
        {
            throw new DomainRuleException("ASSET_ALREADY_ASSIGNED", "The asset already has an active assignment.");
        }

        return await GetAsync(assetId, cancellationToken);
    }

    public async Task<AssetDto> ReturnAsync(
        Guid assetId,
        ReturnAssetRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var asset = await FindForUpdateAsync(assetId, cancellationToken);
        SetExpectedVersion(asset, request.ExpectedVersion);
        AssetStateMachine.EnsureTransition(asset.State, AssetState.InStock);

        var assignment = asset.Assignments.SingleOrDefault(x => x.ReturnedAt is null)
            ?? throw new DomainRuleException("ACTIVE_ASSIGNMENT_NOT_FOUND", "The asset has no active assignment.");
        var now = timeProvider.GetUtcNow();
        assignment.ReturnedAt = now;
        assignment.ReturnNotes = request.Notes?.Trim();
        asset.State = AssetState.InStock;
        asset.UpdatedAt = now;
        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.Returned, request.Actor, correlationId, new
        {
            assignment.Id,
            assignment.EmployeeId,
            request.Notes
        }));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(assetId, cancellationToken);
    }

    public async Task<AssetDto> MoveAsync(
        Guid assetId,
        MoveAssetRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var asset = await FindForUpdateAsync(assetId, cancellationToken);
        SetExpectedVersion(asset, request.ExpectedVersion);
        if (asset.State != AssetState.InStock)
        {
            throw new DomainRuleException(
                "ASSET_NOT_MOVABLE",
                "Only an in-stock asset can be moved between departments.");
        }

        _ = await db.Departments.FindAsync([request.DepartmentId], cancellationToken)
            ?? throw new ResourceNotFoundException(nameof(Department), request.DepartmentId);
        var previousDepartmentId = asset.DepartmentId;
        asset.DepartmentId = request.DepartmentId;
        asset.UpdatedAt = timeProvider.GetUtcNow();
        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.DepartmentMoved, request.Actor, correlationId, new
        {
            previousDepartmentId,
            request.DepartmentId
        }));

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(assetId, cancellationToken);
    }

    public async Task<AssetDto> StartRepairAsync(
        Guid assetId,
        StartRepairRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var asset = await FindForUpdateAsync(assetId, cancellationToken);
        SetExpectedVersion(asset, request.ExpectedVersion);
        AssetStateMachine.EnsureTransition(asset.State, AssetState.InRepair);
        var now = timeProvider.GetUtcNow();
        asset.State = AssetState.InRepair;
        asset.UpdatedAt = now;
        db.MaintenanceRecords.Add(new Maintenance
        {
            AssetId = asset.Id,
            Description = request.Description.Trim(),
            Vendor = request.Vendor?.Trim(),
            Cost = request.Cost,
            StartedAt = now
        });
        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.RepairStarted, request.Actor, correlationId, new
        {
            request.Description,
            request.Vendor,
            request.Cost
        }));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(assetId, cancellationToken);
    }

    public async Task<AssetDto> CompleteRepairAsync(
        Guid assetId,
        CompleteRepairRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var asset = await FindForUpdateAsync(assetId, cancellationToken);
        SetExpectedVersion(asset, request.ExpectedVersion);
        AssetStateMachine.EnsureTransition(asset.State, AssetState.InStock);
        var maintenance = asset.MaintenanceRecords
            .Where(x => x.Status == MaintenanceStatus.Open)
            .OrderByDescending(x => x.StartedAt)
            .FirstOrDefault() ?? throw new DomainRuleException("OPEN_REPAIR_NOT_FOUND", "No open repair exists.");
        var now = timeProvider.GetUtcNow();
        maintenance.Status = MaintenanceStatus.Completed;
        maintenance.CompletedAt = now;
        asset.State = AssetState.InStock;
        asset.UpdatedAt = now;
        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.RepairCompleted, request.Actor, correlationId, new
        {
            maintenance.Id
        }));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(assetId, cancellationToken);
    }

    public async Task<AssetDto> RetireAsync(
        Guid assetId,
        RetireAssetRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var asset = await FindForUpdateAsync(assetId, cancellationToken);
        SetExpectedVersion(asset, request.ExpectedVersion);
        AssetStateMachine.EnsureTransition(asset.State, AssetState.Retired);
        var now = timeProvider.GetUtcNow();
        asset.State = AssetState.Retired;
        asset.RetiredAt = now;
        asset.UpdatedAt = now;
        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.Retired, request.Actor, correlationId, new
        {
            request.Reason
        }));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(assetId, cancellationToken);
    }

    public async Task<AssetDto> UpsertWarrantyAsync(
        Guid assetId,
        UpsertWarrantyRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (request.EndDate < request.StartDate)
        {
            throw new DomainRuleException("INVALID_WARRANTY_PERIOD", "Warranty end date cannot precede its start date.");
        }

        var asset = await FindForUpdateAsync(assetId, cancellationToken);
        SetExpectedVersion(asset, request.ExpectedVersion);
        var now = timeProvider.GetUtcNow();
        if (asset.Warranty is null)
        {
            asset.Warranty = new Warranty
            {
                AssetId = asset.Id,
                Provider = request.Provider.Trim(),
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                CreatedAt = now
            };
            db.Warranties.Add(asset.Warranty);
        }

        asset.Warranty.Provider = request.Provider.Trim();
        asset.Warranty.StartDate = request.StartDate;
        asset.Warranty.EndDate = request.EndDate;
        asset.Warranty.CoverageNotes = request.CoverageNotes?.Trim();
        asset.Warranty.UpdatedAt = now;
        asset.UpdatedAt = now;
        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.WarrantyRecorded, request.Actor, correlationId, new
        {
            request.Provider,
            request.StartDate,
            request.EndDate
        }));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(assetId, cancellationToken);
    }

    public async Task<AssetDto> InstallSoftwareAsync(
        Guid assetId,
        InstallSoftwareRequest request,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var asset = await FindForUpdateAsync(assetId, cancellationToken);
        SetExpectedVersion(asset, request.ExpectedVersion);
        if (asset.State == AssetState.Retired)
        {
            throw new DomainRuleException("ASSET_RETIRED", "Software cannot be installed on a retired asset.");
        }

        var now = timeProvider.GetUtcNow();
        db.SoftwareInstallations.Add(new SoftwareInstallation
        {
            AssetId = asset.Id,
            Name = request.Name.Trim(),
            Version = request.Version.Trim(),
            InstalledAt = now
        });
        asset.UpdatedAt = now;
        db.AssetEvents.Add(NewEvent(asset.Id, AssetEventType.SoftwareInstalled, request.Actor, correlationId, new
        {
            request.Name,
            request.Version
        }));
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(assetId, cancellationToken);
    }

    public async Task<AssetDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await AssetQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException(nameof(Asset), id);
        return AssetMapper.ToDto(asset);
    }

    private async Task<Asset> FindForUpdateAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Assets
            .Include(x => x.Assignments)
            .Include(x => x.MaintenanceRecords)
            .Include(x => x.Warranty)
            .Include(x => x.SoftwareInstallations)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
        ?? throw new ResourceNotFoundException(nameof(Asset), id);

    private IQueryable<Asset> AssetQuery() => db.Assets
        .AsNoTracking()
        .Include(x => x.Department)
        .Include(x => x.Warranty)
        .Include(x => x.Assignments.Where(a => a.ReturnedAt == null))
        .ThenInclude(x => x.Employee);

    private void SetExpectedVersion(Asset asset, uint expectedVersion)
    {
        db.Entry(asset).Property(x => x.Version).OriginalValue = expectedVersion;
    }

    private AssetEvent NewEvent(
        Guid assetId,
        AssetEventType type,
        string actor,
        string? correlationId,
        object data) => new()
        {
            AssetId = assetId,
            Type = type,
            Actor = actor.Trim(),
            CorrelationId = correlationId,
            OccurredAt = timeProvider.GetUtcNow(),
            Data = JsonSerializer.Serialize(data)
        };
}
