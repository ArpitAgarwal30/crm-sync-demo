namespace CrmSync.Core.Sync;

public sealed record AuditEntry(
    DateTimeOffset At,
    string RunId,
    string Event,
    string SourceId,
    int? TargetId,
    string Detail);

/// <summary>Turns runs and rollbacks into flat, append-only audit entries.</summary>
public static class AuditLog
{
    public static IEnumerable<AuditEntry> ForRun(SyncRun run)
    {
        foreach (var change in run.Applied)
        {
            var fields = change.Changes.Count == 0
                ? "linked only"
                : string.Join("; ", change.Changes.Select(c => c.Describe()));
            var eventName = change.Action == ChangeAction.Conflict ? "ConflictOverwritten" : change.Action.ToString();
            yield return new(run.AppliedAt, run.RunId, eventName, change.SourceId, change.TargetId, fields);
        }

        foreach (var skip in run.Skipped)
        {
            yield return new(run.AppliedAt, run.RunId, $"Skipped{skip.Action}", skip.SourceId, null, skip.Reason);
        }
    }

    public static IEnumerable<AuditEntry> ForRollback(RollbackResult result)
    {
        var at = result.Run.RolledBackAt!.Value;
        foreach (var id in result.Restored)
        {
            var source = result.Run.Applied.First(a => a.TargetId == id).SourceId;
            yield return new(at, result.Run.RunId, "RolledBack", source, id, "Restored to its state before the run");
        }

        foreach (var refusal in result.Refused)
        {
            var source = result.Run.Applied.First(a => a.TargetId == refusal.TargetId).SourceId;
            yield return new(at, result.Run.RunId, "RollbackRefused", source, refusal.TargetId, refusal.Reason);
        }
    }
}
