namespace EnterpriseAssetLifecycle.Domain;

public static class AssetStateMachine
{
    private static readonly IReadOnlyDictionary<AssetState, IReadOnlySet<AssetState>> AllowedTransitions =
        new Dictionary<AssetState, IReadOnlySet<AssetState>>
        {
            [AssetState.InStock] = new HashSet<AssetState> { AssetState.Assigned, AssetState.InRepair, AssetState.Retired },
            [AssetState.Assigned] = new HashSet<AssetState> { AssetState.InStock },
            [AssetState.InRepair] = new HashSet<AssetState> { AssetState.InStock, AssetState.Retired },
            [AssetState.Retired] = new HashSet<AssetState>()
        };

    public static bool CanTransition(AssetState from, AssetState to) =>
        from != to && AllowedTransitions[from].Contains(to);

    public static void EnsureTransition(AssetState from, AssetState to)
    {
        if (!CanTransition(from, to))
        {
            throw new DomainRuleException(
                "INVALID_ASSET_STATE_TRANSITION",
                $"Asset cannot transition from {from} to {to}.");
        }
    }
}

public sealed class DomainRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class ResourceNotFoundException(string resource, object key)
    : Exception($"{resource} '{key}' was not found.")
{
    public string Resource { get; } = resource;
}

public sealed class IdempotencyConflictException(string message) : Exception(message);

