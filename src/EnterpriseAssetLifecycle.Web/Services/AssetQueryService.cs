using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Domain;
using EnterpriseAssetLifecycle.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Services;

public sealed class AssetQueryService(AssetDbContext db, TimeProvider timeProvider)
{
    public async Task<PageResult<AssetDto>> ListAsync(
        string? search,
        AssetState? state,
        AssetType? type,
        Guid? departmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Assets
            .AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Warranty)
            .Include(x => x.Assignments.Where(a => a.ReturnedAt == null))
            .ThenInclude(x => x.Employee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.AssetTag, pattern) ||
                EF.Functions.ILike(x.SerialNumber, pattern) ||
                EF.Functions.ILike(x.Manufacturer, pattern) ||
                EF.Functions.ILike(x.Model, pattern));
        }

        if (state is not null)
        {
            query = query.Where(x => x.State == state);
        }

        if (type is not null)
        {
            query = query.Where(x => x.Type == type);
        }

        if (departmentId is not null)
        {
            query = query.Where(x => x.DepartmentId == departmentId);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.AssetTag)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        return new PageResult<AssetDto>(items.Select(AssetMapper.ToDto).ToList(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<AssetEventDto>> EventsAsync(Guid assetId, CancellationToken cancellationToken)
    {
        if (!await db.Assets.AnyAsync(x => x.Id == assetId, cancellationToken))
        {
            throw new ResourceNotFoundException(nameof(Asset), assetId);
        }

        return await db.AssetEvents
            .AsNoTracking()
            .Where(x => x.AssetId == assetId)
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new AssetEventDto(x.Id, x.Type, x.Actor, x.OccurredAt, x.Data, x.CorrelationId))
            .ToListAsync(cancellationToken);
    }

    public async Task<DashboardDto> DashboardAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var warningDate = today.AddDays(30);
        var now = timeProvider.GetUtcNow();
        var counts = await db.Assets
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Total = group.Count(),
                InStock = group.Count(x => x.State == AssetState.InStock),
                Assigned = group.Count(x => x.State == AssetState.Assigned),
                InRepair = group.Count(x => x.State == AssetState.InRepair),
                Retired = group.Count(x => x.State == AssetState.Retired)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var expiring = await db.Warranties.CountAsync(
            x => x.EndDate >= today && x.EndDate <= warningDate,
            cancellationToken);
        var overdue = await db.Assignments.CountAsync(
            x => x.ReturnedAt == null && x.ExpectedReturnAt != null && x.ExpectedReturnAt < now,
            cancellationToken);
        return new DashboardDto(
            counts?.Total ?? 0,
            counts?.InStock ?? 0,
            counts?.Assigned ?? 0,
            counts?.InRepair ?? 0,
            counts?.Retired ?? 0,
            expiring,
            overdue);
    }
}

