using CrmSync.Core.Target;

namespace CrmSync.Core.Sync;

public sealed class SyncExecutor(ITargetCrm target, SyncState state, TimeProvider clock)
{
    /// <summary>
    /// Applies a plan. Conflicts are skipped unless <paramref name="sourceWinsConflicts"/> is set, and any
    /// change whose target moved since planning is skipped as stale, so replaying an old plan is safe.
    /// </summary>
    public SyncRun Apply(SyncPlan plan, bool sourceWinsConflicts = false)
    {
        var applied = new List<AppliedChange>();
        var skipped = new List<SkippedChange>();

        foreach (var change in plan.Changes)
        {
            switch (change.Action)
            {
                case ChangeAction.Unchanged:
                    continue;
                case ChangeAction.Invalid:
                    skipped.Add(new(change.SourceId, change.Action, change.Reason ?? "Invalid source record"));
                    continue;
                case ChangeAction.Conflict when !sourceWinsConflicts:
                    skipped.Add(new(change.SourceId, change.Action, $"{change.Reason}; rerun with source-wins to overwrite"));
                    continue;
                case ChangeAction.Create:
                    if (target.FindByExternalRef(change.SourceId) is not null)
                    {
                        skipped.Add(new(change.SourceId, change.Action, "Stale: already exists in Beta CRM"));
                        continue;
                    }

                    var created = target.Create(change.Desired!, change.SourceId);
                    state.RecordWrite(created);
                    applied.Add(new AppliedChange
                    {
                        Action = change.Action,
                        SourceId = change.SourceId,
                        TargetId = created.Id,
                        After = created,
                        Changes = change.Changes,
                    });
                    continue;
            }

            // Update, or a conflict where the source wins.
            var current = target.Find(change.TargetId!.Value);
            if (current is null || current.ModifiedAt != change.ExpectedTargetVersion)
            {
                skipped.Add(new(change.SourceId, change.Action, "Stale: the Beta contact changed after the plan was made"));
                continue;
            }

            var updated = target.Update(current.Id, change.Desired!, change.SourceId);
            state.RecordWrite(updated);
            applied.Add(new AppliedChange
            {
                Action = change.Action,
                SourceId = change.SourceId,
                TargetId = updated.Id,
                Before = current,
                After = updated,
                Changes = change.Changes,
            });
        }

        var now = clock.GetUtcNow();
        return new SyncRun
        {
            RunId = $"{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}",
            AppliedAt = now,
            Applied = applied,
            Skipped = skipped,
        };
    }

    /// <summary>
    /// Undoes a run, newest change first. Records edited after the sync are left alone and reported,
    /// so a rollback never overwrites someone else's work. Created records are removed; updated
    /// records get their previous values back.
    /// </summary>
    public RollbackResult Rollback(SyncRun run)
    {
        if (run.RolledBackAt is not null)
        {
            throw new InvalidOperationException($"Run {run.RunId} was already rolled back.");
        }

        var restored = new List<int>();
        var refused = new List<RefusedRestore>();

        foreach (var entry in run.Applied.Reverse())
        {
            var current = target.Find(entry.TargetId);
            if (current is null)
            {
                refused.Add(new(entry.TargetId, "No longer exists in Beta CRM"));
                continue;
            }

            if (current.ModifiedAt != entry.After.ModifiedAt)
            {
                refused.Add(new(entry.TargetId, "Edited after the sync; left as is"));
                continue;
            }

            if (entry.Before is null)
            {
                target.Delete(current.Id);
                state.LastWritten.Remove(current.Id);
            }
            else
            {
                state.RecordWrite(target.Update(current.Id, BetaAdapter.FieldsOf(entry.Before), entry.Before.ExternalRef));
            }

            restored.Add(entry.TargetId);
        }

        return new RollbackResult(run with { RolledBackAt = clock.GetUtcNow() }, restored, refused);
    }
}
