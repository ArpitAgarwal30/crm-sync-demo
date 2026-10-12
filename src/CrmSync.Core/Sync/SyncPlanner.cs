using CrmSync.Core.Model;
using CrmSync.Core.Source;
using CrmSync.Core.Target;

namespace CrmSync.Core.Sync;

public static class SyncPlanner
{
    /// <summary>
    /// Matches each source record to a target record (by link first, then by email for unlinked
    /// records) and decides what should happen. Target records with no source match are left alone:
    /// the sync never deletes.
    /// </summary>
    public static SyncPlan Plan(IReadOnlyList<AlphaContact> source, IReadOnlyList<BetaContact> target, SyncState state)
    {
        var linked = target
            .Where(t => t.ExternalRef is not null)
            .GroupBy(t => t.ExternalRef!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var unlinkedByEmail = target
            .Where(t => t.ExternalRef is null && Normalize.Email(t.Email) is not null)
            .GroupBy(t => Normalize.Email(t.Email)!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var claimed = new HashSet<int>();
        var changes = new List<PlannedChange>();

        foreach (var record in source)
        {
            var name = $"{record.FirstName} {record.LastName}".Trim();
            var mapped = AlphaAdapter.ToCanonical(record);
            if (mapped.Contact is not { } contact)
            {
                changes.Add(new PlannedChange { Action = ChangeAction.Invalid, SourceId = record.Id, DisplayName = name, Reason = mapped.Error });
                continue;
            }

            var desired = BetaAdapter.ToFields(contact);
            var existing = linked.GetValueOrDefault(record.Id) ?? unlinkedByEmail.GetValueOrDefault(contact.Email);

            if (existing is null)
            {
                changes.Add(new PlannedChange
                {
                    Action = ChangeAction.Create,
                    SourceId = record.Id,
                    DisplayName = name,
                    Desired = desired,
                    Changes = BetaAdapter.Diff(null, desired),
                });
                continue;
            }

            if (!claimed.Add(existing.Id))
            {
                changes.Add(new PlannedChange
                {
                    Action = ChangeAction.Invalid,
                    SourceId = record.Id,
                    DisplayName = name,
                    TargetId = existing.Id,
                    Reason = $"Matches Beta contact {existing.Id}, already matched by another source record",
                });
                continue;
            }

            var diff = BetaAdapter.Diff(BetaAdapter.FieldsOf(existing), desired);
            var needsLink = existing.ExternalRef is null;
            if (diff.Count == 0 && !needsLink)
            {
                changes.Add(new PlannedChange { Action = ChangeAction.Unchanged, SourceId = record.Id, DisplayName = name, TargetId = existing.Id });
                continue;
            }

            var conflict = diff.Count > 0 && state.EditedSinceLastSync(existing);
            changes.Add(new PlannedChange
            {
                Action = conflict ? ChangeAction.Conflict : ChangeAction.Update,
                SourceId = record.Id,
                DisplayName = name,
                TargetId = existing.Id,
                ExpectedTargetVersion = existing.ModifiedAt,
                Desired = desired,
                Changes = diff,
                Reason = conflict ? "Edited in Beta CRM since the last sync"
                    : needsLink ? "Matched by email; will be linked to the source record"
                    : null,
            });
        }

        var unmanaged = target.Where(t => !claimed.Contains(t.Id)).Select(t => t.Id).ToList();
        return new SyncPlan(changes, unmanaged);
    }
}
