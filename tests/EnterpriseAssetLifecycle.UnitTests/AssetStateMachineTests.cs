using EnterpriseAssetLifecycle.Domain;
using Xunit;

namespace EnterpriseAssetLifecycle.UnitTests;

public sealed class AssetStateMachineTests
{
    public static TheoryData<AssetState, AssetState> AllowedTransitions => new()
    {
        { AssetState.InStock, AssetState.Assigned },
        { AssetState.InStock, AssetState.InRepair },
        { AssetState.InStock, AssetState.Retired },
        { AssetState.Assigned, AssetState.InStock },
        { AssetState.InRepair, AssetState.InStock },
        { AssetState.InRepair, AssetState.Retired }
    };

    public static TheoryData<AssetState, AssetState> RejectedTransitions => new()
    {
        { AssetState.InStock, AssetState.InStock },
        { AssetState.Assigned, AssetState.InRepair },
        { AssetState.Assigned, AssetState.Retired },
        { AssetState.InRepair, AssetState.Assigned },
        { AssetState.Retired, AssetState.InStock },
        { AssetState.Retired, AssetState.Assigned },
        { AssetState.Retired, AssetState.InRepair }
    };

    [Theory]
    [MemberData(nameof(AllowedTransitions))]
    public void CanTransition_ReturnsTrue_ForAllowedTransition(AssetState from, AssetState to)
    {
        Assert.True(AssetStateMachine.CanTransition(from, to));
        AssetStateMachine.EnsureTransition(from, to);
    }

    [Theory]
    [MemberData(nameof(RejectedTransitions))]
    public void EnsureTransition_ThrowsDomainRule_ForRejectedTransition(AssetState from, AssetState to)
    {
        var exception = Assert.Throws<DomainRuleException>(() => AssetStateMachine.EnsureTransition(from, to));
        Assert.Equal("INVALID_ASSET_STATE_TRANSITION", exception.Code);
        Assert.Contains(from.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(to.ToString(), exception.Message, StringComparison.Ordinal);
    }
}
