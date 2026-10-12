using CrmSync.Core.Target;

namespace CrmSync.Core.Sync;

/// <summary>
/// Remembers the ModifiedAt of every target record as the sync last wrote it. If a record's
/// ModifiedAt has moved since, someone edited it in the target CRM: the basis for conflict detection.
/// </summary>
public sealed class SyncState
{
    public Dictionary<int, DateTimeOffset> LastWritten { get; init; } = [];

    public bool EditedSinceLastSync(BetaContact record) =>
        LastWritten.TryGetValue(record.Id, out var written) && written != record.ModifiedAt;

    public void RecordWrite(BetaContact record) => LastWritten[record.Id] = record.ModifiedAt;
}
