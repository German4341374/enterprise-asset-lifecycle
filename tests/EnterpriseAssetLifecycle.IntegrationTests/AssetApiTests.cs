using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EnterpriseAssetLifecycle.Contracts;
using EnterpriseAssetLifecycle.Domain;
using EnterpriseAssetLifecycle.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EnterpriseAssetLifecycle.IntegrationTests;

[Collection(PostgresCollection.Name)]
public sealed class AssetApiTests(PostgresFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public async Task Readiness_UsesRealPostgres_AndReturnsHealthy()
    {
        var response = await fixture.Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AssignmentAndReturn_CreateCustodyHistoryAndAuditEvents()
    {
        var asset = await CreateAssetAsync("FLOW");
        var employees = await fixture.Client.GetFromJsonAsync<List<Employee>>("/api/employees", JsonOptions);
        var employee = Assert.Single(employees!, x => x.EmployeeNumber == "EMP-1001");

        var assignResponse = await fixture.Client.PostAsJsonAsync(
            $"/api/assets/{asset.Id}/assign",
            new AssignAssetRequest(employee.Id, DateTimeOffset.UtcNow.AddDays(7), asset.Version, "integration-test"),
            JsonOptions);
        assignResponse.EnsureSuccessStatusCode();
        var assigned = await assignResponse.Content.ReadFromJsonAsync<AssetDto>(JsonOptions);
        Assert.Equal(AssetState.Assigned, assigned!.State);
        Assert.Equal(employee.Id, assigned.ActiveAssignment!.EmployeeId);

        var returnResponse = await fixture.Client.PostAsJsonAsync(
            $"/api/assets/{asset.Id}/return",
            new ReturnAssetRequest(assigned.Version, "integration-test", "Test return"),
            JsonOptions);
        returnResponse.EnsureSuccessStatusCode();
        var returned = await returnResponse.Content.ReadFromJsonAsync<AssetDto>(JsonOptions);
        Assert.Equal(AssetState.InStock, returned!.State);
        Assert.Null(returned.ActiveAssignment);

        var events = await fixture.Client.GetFromJsonAsync<List<AssetEventDto>>(
            $"/api/assets/{asset.Id}/events",
            JsonOptions);
        Assert.Contains(events!, x => x.Type == AssetEventType.Assigned);
        Assert.Contains(events!, x => x.Type == AssetEventType.Returned);
    }

    [Fact]
    public async Task StaleVersion_ReturnsProblemDetailsConflict()
    {
        var asset = await CreateAssetAsync("STALE");
        var departments = await fixture.Client.GetFromJsonAsync<List<Department>>("/api/departments", JsonOptions);
        var target = Assert.Single(departments!, x => x.Id != asset.DepartmentId && x.Code == "ENG");

        var first = await fixture.Client.PostAsJsonAsync(
            $"/api/assets/{asset.Id}/move",
            new MoveAssetRequest(target.Id, asset.Version, "integration-test"),
            JsonOptions);
        first.EnsureSuccessStatusCode();

        var stale = await fixture.Client.PostAsJsonAsync(
            $"/api/assets/{asset.Id}/move",
            new MoveAssetRequest(asset.DepartmentId, asset.Version, "stale-client"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CONCURRENCY_CONFLICT", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task RetiredAsset_CannotBeAssigned()
    {
        var asset = await CreateAssetAsync("RETIRED");
        var retire = await fixture.Client.PostAsJsonAsync(
            $"/api/assets/{asset.Id}/retire",
            new RetireAssetRequest(asset.Version, "integration-test", "End of life"),
            JsonOptions);
        retire.EnsureSuccessStatusCode();
        var retired = await retire.Content.ReadFromJsonAsync<AssetDto>(JsonOptions);
        var employees = await fixture.Client.GetFromJsonAsync<List<Employee>>("/api/employees", JsonOptions);

        var assign = await fixture.Client.PostAsJsonAsync(
            $"/api/assets/{asset.Id}/assign",
            new AssignAssetRequest(employees![0].Id, null, retired!.Version, "integration-test"),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode);
    }

    [Fact]
    public async Task CsvImport_ReplaysSameKey_AndRejectsChangedPayload()
    {
        var key = $"import-{Guid.NewGuid():N}";
        var tag = $"CSV-{Guid.NewGuid():N}"[..16];
        var csv = $"assetTag,type,manufacturer,model,serialNumber,departmentCode,purchaseDate\n{tag},Laptop,Example,CSV Device,SN-{Guid.NewGuid():N},OPS,2026-01-15\n";

        var first = await ImportAsync(key, csv);
        first.EnsureSuccessStatusCode();
        var firstResult = await first.Content.ReadFromJsonAsync<ImportResultDto>(JsonOptions);
        Assert.Equal(1, firstResult!.ImportedRows);
        Assert.False(firstResult.Replayed);

        var replay = await ImportAsync(key, csv);
        replay.EnsureSuccessStatusCode();
        var replayResult = await replay.Content.ReadFromJsonAsync<ImportResultDto>(JsonOptions);
        Assert.True(replayResult!.Replayed);
        Assert.Equal(firstResult.BatchId, replayResult.BatchId);

        var changed = await ImportAsync(key, csv.Replace("Laptop", "Desktop", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.Conflict, changed.StatusCode);
    }

    [Fact]
    public async Task DatabaseTrigger_RejectsAuditEventMutation()
    {
        var asset = await CreateAssetAsync("AUDIT");
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssetDbContext>();
        await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE \"AssetEvents\" SET \"Actor\" = 'tampered' WHERE \"AssetId\" = {asset.Id}"));
    }

    [Fact]
    public async Task Export_ReturnsCsvWithExpectedHeader()
    {
        var response = await fixture.Client.GetAsync("/api/assets/export.csv");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        var csv = await response.Content.ReadAsStringAsync();
        Assert.Contains("AssetTag,Type,Manufacturer", csv, StringComparison.Ordinal);
    }

    private async Task<AssetDto> CreateAssetAsync(string prefix)
    {
        var departments = await fixture.Client.GetFromJsonAsync<List<Department>>("/api/departments", JsonOptions);
        var operations = Assert.Single(departments!, x => x.Code == "OPS");
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var response = await fixture.Client.PostAsJsonAsync(
            "/api/assets",
            new CreateAssetRequest(
                $"{prefix}-{suffix}",
                AssetType.Laptop,
                "Integration Vendor",
                "Integration Model",
                $"SN-{prefix}-{suffix}",
                operations.Id,
                new DateOnly(2026, 1, 1)),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AssetDto>(JsonOptions))!;
    }

    private async Task<HttpResponseMessage> ImportAsync(string key, string csv)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(csv, Encoding.UTF8, "text/csv"), "file", "assets.csv");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/imports/assets") { Content = content };
        request.Headers.Add("Idempotency-Key", key);
        request.Headers.Add("X-Actor", "integration-test");
        return await fixture.Client.SendAsync(request);
    }
}
