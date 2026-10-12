using CrmSync.Core.Target;

namespace CrmSync.Core.Sync;

public enum ChangeAction
{
    Create,
    Update,
    Unchanged,
    Conflict,
    Invalid,
}

public sealed record PlannedChange
{
    public required ChangeAction Action { get; init; }

    public required string SourceId { get; init; }

    /// <summary>A human-readable name for reports, available even for records that failed validation.</summary>
    public required string DisplayName { get; init; }

    public int? TargetId { get; init; }

    /// <summary>The target's ModifiedAt when the plan was made. Applying is refused if it has moved since.</summary>
    public DateTimeOffset? ExpectedTargetVersion { get; init; }

    public BetaFields? Desired { get; init; }

    public IReadOnlyList<FieldChange> Changes { get; init; } = [];

    public string? Reason { get; init; }
}

/// <summary>A dry run: everything the sync would do, computed without changing anything.</summary>
public sealed record SyncPlan(IReadOnlyList<PlannedChange> Changes, IReadOnlyList<int> UnmanagedTargetIds)
{
    public int Count(ChangeAction action) => Changes.Count(c => c.Action == action);
}
