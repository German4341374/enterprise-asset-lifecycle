using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Infrastructure;
using EnterpriseAssetLifecycle.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Pages.Assets;

public sealed class DetailsModel(
    AssetDbContext db,
    AssetLifecycleService lifecycle,
    AssetQueryService queries) : PageModel
{
    public AssetDto Asset { get; private set; } = null!;
    public IReadOnlyList<AssetEventDto> Events { get; private set; } = [];
    public List<SelectListItem> Employees { get; private set; } = [];
    public List<SelectListItem> Departments { get; private set; } = [];
    [BindProperty] public Guid EmployeeId { get; set; }
    [BindProperty] public Guid DepartmentId { get; set; }
    [BindProperty] public DateTimeOffset? ExpectedReturnAt { get; set; }
    [BindProperty] public string? RepairDescription { get; set; }
    [BindProperty] public string? Reason { get; set; }
    [BindProperty] public uint ExpectedVersion { get; set; }

    public async Task OnGetAsync(Guid id, CancellationToken cancellationToken) => await LoadAsync(id, cancellationToken);

    public async Task<IActionResult> OnPostAssignAsync(Guid id, CancellationToken cancellationToken)
    {
        await lifecycle.AssignAsync(id, new AssignAssetRequest(EmployeeId, ExpectedReturnAt, ExpectedVersion, "web-ui"), HttpContext.TraceIdentifier, cancellationToken);
        TempData["Message"] = "Asset assigned.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReturnAsync(Guid id, CancellationToken cancellationToken)
    {
        await lifecycle.ReturnAsync(id, new ReturnAssetRequest(ExpectedVersion, "web-ui", "Returned from web UI"), HttpContext.TraceIdentifier, cancellationToken);
        TempData["Message"] = "Asset returned to stock.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostMoveAsync(Guid id, CancellationToken cancellationToken)
    {
        await lifecycle.MoveAsync(id, new MoveAssetRequest(DepartmentId, ExpectedVersion, "web-ui"), HttpContext.TraceIdentifier, cancellationToken);
        TempData["Message"] = "Asset moved.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStartRepairAsync(Guid id, CancellationToken cancellationToken)
    {
        await lifecycle.StartRepairAsync(id, new StartRepairRequest(RepairDescription ?? "Inspection", null, null, ExpectedVersion, "web-ui"), HttpContext.TraceIdentifier, cancellationToken);
        TempData["Message"] = "Repair started.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCompleteRepairAsync(Guid id, CancellationToken cancellationToken)
    {
        await lifecycle.CompleteRepairAsync(id, new CompleteRepairRequest(ExpectedVersion, "web-ui"), HttpContext.TraceIdentifier, cancellationToken);
        TempData["Message"] = "Repair completed.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRetireAsync(Guid id, CancellationToken cancellationToken)
    {
        await lifecycle.RetireAsync(id, new RetireAssetRequest(ExpectedVersion, "web-ui", Reason ?? "Lifecycle completed"), HttpContext.TraceIdentifier, cancellationToken);
        TempData["Message"] = "Asset retired.";
        return RedirectToPage(new { id });
    }

    private async Task LoadAsync(Guid id, CancellationToken cancellationToken)
    {
        Asset = await lifecycle.GetAsync(id, cancellationToken);
        ExpectedVersion = Asset.Version;
        Events = await queries.EventsAsync(id, cancellationToken);
        Employees = await db.Employees.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.FullName)
            .Select(x => new SelectListItem(x.FullName, x.Id.ToString())).ToListAsync(cancellationToken);
        Departments = await db.Departments.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(cancellationToken);
    }
}

