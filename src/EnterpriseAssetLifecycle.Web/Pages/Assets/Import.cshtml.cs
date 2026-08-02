using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EnterpriseAssetLifecycle.Pages.Assets;

public sealed class ImportModel(AssetCsvService csvService) : PageModel
{
    [BindProperty] public IFormFile? CsvFile { get; set; }
    [BindProperty] public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    public ImportResultDto? Result { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (CsvFile is null)
        {
            ModelState.AddModelError(nameof(CsvFile), "Select a CSV file.");
            return Page();
        }

        await using var stream = CsvFile.OpenReadStream();
        Result = await csvService.ImportAsync(stream, CsvFile.Length, IdempotencyKey, "web-ui", cancellationToken);
        return Page();
    }
}

