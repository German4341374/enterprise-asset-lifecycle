using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Domain;
using EnterpriseAssetLifecycle.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EnterpriseAssetLifecycle.Pages.Assets;

public sealed class IndexModel(AssetQueryService queries) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public AssetState? State { get; set; }
    [BindProperty(SupportsGet = true)] public AssetType? Type { get; set; }
    [BindProperty(SupportsGet = true)] public int PageNumber { get; set; } = 1;
    public PageResult<AssetDto> Results { get; private set; } = new([], 1, 25, 0);

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Results = await queries.ListAsync(Search, State, Type, null, PageNumber, 25, cancellationToken);
    }
}

