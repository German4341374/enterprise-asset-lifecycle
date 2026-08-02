using EnterpriseAssetLifecycle.Domain;
using Xunit;

namespace EnterpriseAssetLifecycle.UnitTests;

public sealed class EntityDefaultsTests
{
    [Fact]
    public void NewAsset_StartsInStock()
    {
        var asset = new Asset
        {
            AssetTag = "TEST-1",
            Type = AssetType.Laptop,
            Manufacturer = "Example",
            Model = "Model",
            SerialNumber = "SERIAL-1"
        };

        Assert.Equal(AssetState.InStock, asset.State);
        Assert.NotEqual(Guid.Empty, asset.Id);
        Assert.Empty(asset.Assignments);
    }

    [Fact]
    public void NewMaintenance_StartsOpen()
    {
        var maintenance = new Maintenance { Description = "Replace keyboard" };
        Assert.Equal(MaintenanceStatus.Open, maintenance.Status);
        Assert.Null(maintenance.CompletedAt);
    }

    [Fact]
    public void DomainException_PreservesMachineReadableCode()
    {
        var exception = new DomainRuleException("TEST_RULE", "Rejected");
        Assert.Equal("TEST_RULE", exception.Code);
        Assert.Equal("Rejected", exception.Message);
    }
}
