using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Domain;
using EnterpriseAssetLifecycle.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseAssetLifecycle.Services;

public sealed class AssetCsvService(AssetDbContext db, TimeProvider timeProvider)
{
    private const int MaxImportBytes = 5 * 1024 * 1024;

    public async Task<ImportResultDto> ImportAsync(
        Stream source,
        long length,
        string idempotencyKey,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 120)
        {
            throw new DomainRuleException(
                "INVALID_IDEMPOTENCY_KEY",
                "Idempotency-Key is required and must contain at most 120 characters.");
        }

        if (length <= 0 || length > MaxImportBytes)
        {
            throw new DomainRuleException("INVALID_IMPORT_SIZE", "CSV imports must be between 1 byte and 5 MB.");
        }

        await using var buffer = new MemoryStream((int)length);
        await source.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var key = idempotencyKey.Trim();

        var previous = await db.ImportBatches.AsNoTracking()
            .SingleOrDefaultAsync(x => x.IdempotencyKey == key, cancellationToken);
        if (previous is not null)
        {
            if (!string.Equals(previous.FileHash, hash, StringComparison.Ordinal))
            {
                throw new IdempotencyConflictException(
                    "This Idempotency-Key was already used with a different file.");
            }

            return ToResult(previous, true);
        }

        var batch = new ImportBatch
        {
            IdempotencyKey = key,
            FileHash = hash,
            StartedAt = timeProvider.GetUtcNow()
        };
        db.ImportBatches.Add(batch);

        var errors = new List<string>();
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            TrimOptions = TrimOptions.Trim,
            PrepareHeaderForMatch = args => args.Header.Trim(),
            MissingFieldFound = null,
            BadDataFound = args => errors.Add($"Malformed CSV near row {args.Context?.Parser?.Row ?? 0}.")
        };

        using var textReader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8, true);
        using var csv = new CsvReader(textReader, config);
        await csv.ReadAsync();
        csv.ReadHeader();
        var rowNumber = 1;

        while (await csv.ReadAsync())
        {
            rowNumber++;
            batch.TotalRows++;
            try
            {
                var row = new AssetImportRow(
                    csv.GetField("assetTag") ?? string.Empty,
                    csv.GetField("type") ?? string.Empty,
                    csv.GetField("manufacturer") ?? string.Empty,
                    csv.GetField("model") ?? string.Empty,
                    csv.GetField("serialNumber") ?? string.Empty,
                    csv.GetField("departmentCode") ?? string.Empty,
                    csv.GetField("purchaseDate"));
                await ImportRowAsync(row, batch, actor, cancellationToken);
            }
            catch (Exception exception) when (exception is FormatException or DomainRuleException)
            {
                batch.FailedRows++;
                errors.Add($"Row {rowNumber}: {exception.Message}");
            }
        }

        batch.Status = batch.FailedRows == 0 ? ImportStatus.Completed : ImportStatus.CompletedWithErrors;
        batch.Errors = JsonSerializer.Serialize(errors);
        batch.CompletedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return ToResult(batch, false);
    }

    public async Task WriteExportAsync(Stream destination, CancellationToken cancellationToken)
    {
        await using var writer = new StreamWriter(destination, new UTF8Encoding(true), leaveOpen: true);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.WriteHeader<AssetExportRow>();
        await csv.NextRecordAsync();

        await foreach (var asset in db.Assets
            .AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Assignments.Where(a => a.ReturnedAt == null))
            .ThenInclude(x => x.Employee)
            .OrderBy(x => x.AssetTag)
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            var active = asset.Assignments.FirstOrDefault();
            csv.WriteRecord(new AssetExportRow(
                SafeCell(asset.AssetTag),
                asset.Type.ToString(),
                SafeCell(asset.Manufacturer),
                SafeCell(asset.Model),
                SafeCell(asset.SerialNumber),
                asset.State.ToString(),
                SafeCell(asset.Department?.Code ?? string.Empty),
                SafeCell(active?.Employee?.FullName ?? string.Empty),
                asset.PurchaseDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty));
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync(cancellationToken);
    }

    private async Task ImportRowAsync(
        AssetImportRow row,
        ImportBatch batch,
        string actor,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(row.AssetTag) ||
            string.IsNullOrWhiteSpace(row.SerialNumber) ||
            string.IsNullOrWhiteSpace(row.Manufacturer) ||
            string.IsNullOrWhiteSpace(row.Model))
        {
            throw new DomainRuleException(
                "MISSING_REQUIRED_FIELD",
                "assetTag, serialNumber, manufacturer and model are required.");
        }

        if (!Enum.TryParse<AssetType>(row.Type, true, out var type))
        {
            throw new DomainRuleException("INVALID_ASSET_TYPE", $"Unknown asset type '{row.Type}'.");
        }

        var tag = row.AssetTag.Trim();
        var serial = row.SerialNumber.Trim();
        if (await db.Assets.AnyAsync(
            x => x.AssetTag == tag || x.SerialNumber == serial,
            cancellationToken))
        {
            batch.SkippedRows++;
            return;
        }

        var departmentCode = row.DepartmentCode.Trim();
        var department = await db.Departments.SingleOrDefaultAsync(
            x => x.Code == departmentCode,
            cancellationToken) ?? throw new DomainRuleException(
                "DEPARTMENT_NOT_FOUND",
                $"Department code '{departmentCode}' does not exist.");

        DateOnly? purchaseDate = null;
        var parsed = default(DateOnly);
        if (!string.IsNullOrWhiteSpace(row.PurchaseDate) &&
            !DateOnly.TryParseExact(row.PurchaseDate.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            throw new DomainRuleException("INVALID_PURCHASE_DATE", "purchaseDate must use yyyy-MM-dd.");
        }
        else if (!string.IsNullOrWhiteSpace(row.PurchaseDate))
        {
            purchaseDate = parsed;
        }

        var now = timeProvider.GetUtcNow();
        var asset = new Asset
        {
            AssetTag = tag,
            Type = type,
            Manufacturer = row.Manufacturer.Trim(),
            Model = row.Model.Trim(),
            SerialNumber = serial,
            DepartmentId = department.Id,
            PurchaseDate = purchaseDate,
            CreatedAt = now,
            UpdatedAt = now
        };
        asset.Events.Add(new AssetEvent
        {
            AssetId = asset.Id,
            Type = AssetEventType.Imported,
            Actor = actor.Trim(),
            OccurredAt = now,
            Data = JsonSerializer.Serialize(new { batch.Id, asset.AssetTag })
        });
        db.Assets.Add(asset);
        batch.ImportedRows++;
    }

    private static ImportResultDto ToResult(ImportBatch batch, bool replayed) => new(
        batch.Id,
        batch.IdempotencyKey,
        batch.Status.ToString(),
        batch.TotalRows,
        batch.ImportedRows,
        batch.SkippedRows,
        batch.FailedRows,
        JsonSerializer.Deserialize<List<string>>(batch.Errors) ?? [],
        replayed);

    private static string SafeCell(string value) =>
        value.Length > 0 && "=+-@".Contains(value[0], StringComparison.Ordinal)
            ? $"'{value}"
            : value;

    private sealed record AssetImportRow(
        string AssetTag,
        string Type,
        string Manufacturer,
        string Model,
        string SerialNumber,
        string DepartmentCode,
        string? PurchaseDate);

    private sealed record AssetExportRow(
        string AssetTag,
        string Type,
        string Manufacturer,
        string Model,
        string SerialNumber,
        string State,
        string Department,
        string AssignedTo,
        string PurchaseDate);
}
