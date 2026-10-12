using CrmSync.Core.Source;
using CrmSync.Core.Sync;
using CrmSync.Core.Target;
using static CrmSync.Core.Tests.TestData;

namespace CrmSync.Core.Tests;

public class RollbackTests
{
    private readonly FakeClock _clock = new(Start.AddHours(1));
    private readonly SyncState _state = new();

    private static readonly AlphaContact[] Source =
    [
        Alpha("A-1", "Grace", "Hopper", "grace@example.com", phone: "555-010-9999"),
        Alpha("A-2", "Alan", "Turing", "alan@example.com"),
    ];

    private (InMemoryTargetCrm Target, SyncExecutor Executor, SyncRun Run) SyncOnce()
    {
        var target = new InMemoryTargetCrm([Beta(5001, "Grace Hopper", "grace@example.com", externalRef: "A-1", phone: "5550100000")], _clock);
        var executor = new SyncExecutor(target, _state, _clock);
        var run = executor.Apply(SyncPlanner.Plan(Source, target.GetAll(), _state));
        _clock.Advance(TimeSpan.FromMinutes(1));
        return (target, executor, run);
    }

    [Fact]
    public void Restores_updated_contacts_and_removes_created_ones()
    {
        var (target, executor, run) = SyncOnce();

        var result = executor.Rollback(run);

        Assert.Equal([5002, 5001], result.Restored);
        Assert.Empty(result.Refused);
        Assert.Equal("5550100000", target.Find(5001)?.PhoneNumber);
        Assert.Null(target.Find(5002));
        Assert.NotNull(result.Run.RolledBackAt);
    }

    [Fact]
    public void After_a_rollback_the_same_changes_are_planned_again_without_false_conflicts()
    {
        var (target, executor, run) = SyncOnce();
        executor.Rollback(run);

        var replan = SyncPlanner.Plan(Source, target.GetAll(), _state);

        Assert.Equal([ChangeAction.Update, ChangeAction.Create], replan.Changes.Select(c => c.Action));
    }

    [Fact]
    public void Leaves_contacts_edited_after_the_sync_untouched()
    {
        var (target, executor, run) = SyncOnce();
        target.Update(5001, new BetaFields("Grace Hopper", "grace@example.com", "5550109999", "Edited after sync", "Prospect"), "A-1");

        var result = executor.Rollback(run);

        Assert.Equal([5002], result.Restored);
        Assert.Equal(5001, Assert.Single(result.Refused).TargetId);
        Assert.Equal("Edited after sync", target.Find(5001)?.Account);
    }

    [Fact]
    public void A_run_cannot_be_rolled_back_twice()
    {
        var (_, executor, run) = SyncOnce();
        var rolledBack = executor.Rollback(run).Run;

        Assert.Throws<InvalidOperationException>(() => executor.Rollback(rolledBack));
    }

    [Fact]
    public void Audit_entries_describe_every_applied_and_restored_change()
    {
        var (_, executor, run) = SyncOnce();

        var applied = AuditLog.ForRun(run).ToList();
        var restored = AuditLog.ForRollback(executor.Rollback(run)).ToList();

        Assert.Equal(["Update", "Create"], applied.Select(e => e.Event));
        Assert.Contains("PhoneNumber: '5550100000' -> '5550109999'", applied[0].Detail);
        Assert.All(restored, e => Assert.Equal("RolledBack", e.Event));
    }
}
