using System.ComponentModel.DataAnnotations;
using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Domain;
using EnterpriseAssetLifecycle.Infrastructure;
using EnterpriseAssetLifecycle.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Pages.Assets;

public sealed class CreateModel(AssetDbContext db, AssetLifecycleService lifecycle) : PageModel
{
    [BindProperty, Required, StringLength(40)] public string AssetTag { get; set; } = string.Empty;
    [BindProperty] public AssetType Type { get; set; }
    [BindProperty, Required, StringLength(100)] public string Manufacturer { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(100)] public string DeviceModel { get; set; } = string.Empty;
    [BindProperty, Required, StringLength(120)] public string SerialNumber { get; set; } = string.Empty;
    [BindProperty, Required] public Guid DepartmentId { get; set; }
    [BindProperty] public DateOnly? PurchaseDate { get; set; }
    public List<SelectListItem> Departments { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadDepartmentsAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadDepartmentsAsync(cancellationToken);
            return Page();
        }

        var asset = await lifecycle.RegisterAsync(
            new CreateAssetRequest(AssetTag, Type, Manufacturer, DeviceModel, SerialNumber, DepartmentId, PurchaseDate),
            "web-ui",
            HttpContext.TraceIdentifier,
            cancellationToken);
        TempData["Message"] = $"Asset {asset.AssetTag} registered.";
        return RedirectToPage("Details", new { id = asset.Id });
    }

    private async Task LoadDepartmentsAsync(CancellationToken cancellationToken)
    {
        Departments = await db.Departments.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new SelectListItem(x.Name, x.Id.ToString())).ToListAsync(cancellationToken);
    }
}

