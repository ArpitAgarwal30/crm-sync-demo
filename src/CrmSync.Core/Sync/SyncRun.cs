using CrmSync.Core.Target;

namespace CrmSync.Core.Sync;

public sealed record AppliedChange
{
    public required ChangeAction Action { get; init; }

    public required string SourceId { get; init; }

    public required int TargetId { get; init; }

    /// <summary>The record before the write, kept for rollback; null when the sync created it.</summary>
    public BetaContact? Before { get; init; }

    public required BetaContact After { get; init; }

    public IReadOnlyList<FieldChange> Changes { get; init; } = [];
}

public sealed record SkippedChange(string SourceId, ChangeAction Action, string Reason);

/// <summary>The record of one applied sync: what changed, what was skipped, and how to undo it.</summary>
public sealed record SyncRun
{
    public required string RunId { get; init; }

    public required DateTimeOffset AppliedAt { get; init; }

    public IReadOnlyList<AppliedChange> Applied { get; init; } = [];

    public IReadOnlyList<SkippedChange> Skipped { get; init; } = [];

    public DateTimeOffset? RolledBackAt { get; init; }
}

public sealed record RefusedRestore(int TargetId, string Reason);

public sealed record RollbackResult(SyncRun Run, IReadOnlyList<int> Restored, IReadOnlyList<RefusedRestore> Refused);
