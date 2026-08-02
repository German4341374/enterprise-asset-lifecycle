using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EnterpriseAssetLifecycle.Pages;

public sealed class IndexModel(AssetQueryService queries) : PageModel
{
    public DashboardDto Dashboard { get; private set; } = new(0, 0, 0, 0, 0, 0, 0);
    public IReadOnlyList<AssetDto> RecentAssets { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Dashboard = await queries.DashboardAsync(cancellationToken);
        RecentAssets = (await queries.ListAsync(null, null, null, null, 1, 5, cancellationToken)).Items;
    }
}

