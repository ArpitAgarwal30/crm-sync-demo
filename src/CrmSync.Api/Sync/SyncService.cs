using System.Text.Json;
using System.Text.Json.Serialization;
using CrmSync.Api.Data;
using CrmSync.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace CrmSync.Api.Sync;

public enum FailureKind
{
    NotFound,
    AlreadyDone,
}

public sealed record Outcome<T>(T? Value, FailureKind? Failure, string? Message);

public static class Outcome
{
    public static Outcome<T> Ok<T>(T value) => new(value, null, null);

    public static Outcome<T> Fail<T>(FailureKind failure, string message) => new(default, failure, message);
}

/// <summary>
/// Wires the pure engine to the database. Apply and rollback each run in one transaction: the
/// target CRM, sync state, run record and audit entries are all saved, or none are.
/// </summary>
public sealed class SyncService(SyncDbContext db, TimeProvider clock)
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new JsonStringEnumConverter() } };

    public PlanResponse CreatePlan()
    {
        var source = db.SourceContacts.AsNoTracking().OrderBy(c => c.Id).ToList();
        var plan = SyncPlanner.Plan(source, Target().GetAll(), LoadState());

        var now = clock.GetUtcNow();
        var record = new PlanRecord { Id = NewId("plan", now), CreatedAt = now, PlanJson = JsonSerializer.Serialize(plan, Json) };
        db.Plans.Add(record);
        db.SaveChanges();
        return PlanResponse.From(record, plan);
    }

    public PlanResponse? GetPlan(string planId) =>
        db.Plans.AsNoTracking().SingleOrDefault(p => p.Id == planId) is { } record
            ? PlanResponse.From(record, ReadPlan(record))
            : null;

    public Outcome<SyncRun> Apply(string planId, bool sourceWinsConflicts)
    {
        using var transaction = db.Database.BeginTransaction();

        var record = db.Plans.SingleOrDefault(p => p.Id == planId);
        if (record is null)
        {
            return Outcome.Fail<SyncRun>(FailureKind.NotFound, $"No plan '{planId}'.");
        }

        if (record.AppliedRunId is not null)
        {
            return Outcome.Fail<SyncRun>(FailureKind.AlreadyDone, $"Plan '{planId}' was already applied as run '{record.AppliedRunId}'. Create a new plan.");
        }

        var state = LoadState();
        var run = new SyncExecutor(Target(), state, clock).Apply(ReadPlan(record), sourceWinsConflicts);

        SaveState(state);
        record.AppliedRunId = run.RunId;
        db.Runs.Add(new RunRecord { RunId = run.RunId, PlanId = planId, AppliedAt = run.AppliedAt, RunJson = JsonSerializer.Serialize(run, Json) });
        db.Audit.AddRange(AuditLog.ForRun(run).Select(ToRow));
        db.SaveChanges();
        transaction.Commit();
        return Outcome.Ok(run);
    }

    public Outcome<RollbackResult> Rollback(string runId)
    {
        using var transaction = db.Database.BeginTransaction();

        var record = db.Runs.SingleOrDefault(r => r.RunId == runId);
        if (record is null)
        {
            return Outcome.Fail<RollbackResult>(FailureKind.NotFound, $"No run '{runId}'.");
        }

        if (record.RolledBackAt is not null)
        {
            return Outcome.Fail<RollbackResult>(FailureKind.AlreadyDone, $"Run '{runId}' was already rolled back.");
        }

        var state = LoadState();
        var result = new SyncExecutor(Target(), state, clock).Rollback(ReadRun(record));

        SaveState(state);
        record.RolledBackAt = result.Run.RolledBackAt;
        record.RunJson = JsonSerializer.Serialize(result.Run, Json);
        db.Audit.AddRange(AuditLog.ForRollback(result).Select(ToRow));
        db.SaveChanges();
        transaction.Commit();
        return Outcome.Ok(result);
    }

    public IReadOnlyList<SyncRun> GetRuns() =>
        [.. db.Runs.AsNoTracking().OrderByDescending(r => r.AppliedAt).AsEnumerable().Select(ReadRun)];

    public SyncRun? GetRun(string runId) =>
        db.Runs.AsNoTracking().SingleOrDefault(r => r.RunId == runId) is { } record ? ReadRun(record) : null;

    private EfTargetCrm Target() => new(db, clock);

    private SyncState LoadState() =>
        new() { LastWritten = db.SyncState.AsNoTracking().ToDictionary(s => s.TargetId, s => s.LastWritten) };

    // Demo-sized: replace the whole table. A production sync would upsert changed rows only.
    private void SaveState(SyncState state)
    {
        db.SyncState.ExecuteDelete();
        db.SyncState.AddRange(state.LastWritten.Select(entry => new SyncStateRow { TargetId = entry.Key, LastWritten = entry.Value }));
    }

    private static SyncPlan ReadPlan(PlanRecord record) =>
        JsonSerializer.Deserialize<SyncPlan>(record.PlanJson, Json) ?? throw new InvalidDataException($"Plan {record.Id} is unreadable.");

    private static SyncRun ReadRun(RunRecord record) =>
        JsonSerializer.Deserialize<SyncRun>(record.RunJson, Json) ?? throw new InvalidDataException($"Run {record.RunId} is unreadable.");

    private static AuditRow ToRow(AuditEntry entry) => new()
    {
        At = entry.At,
        RunId = entry.RunId,
        Event = entry.Event,
        SourceId = entry.SourceId,
        TargetId = entry.TargetId,
        Detail = entry.Detail,
    };

    private static string NewId(string prefix, DateTimeOffset now) => $"{prefix}-{now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..6]}";
}
