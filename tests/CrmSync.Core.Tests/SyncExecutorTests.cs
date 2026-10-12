using CrmSync.Core.Source;
using CrmSync.Core.Sync;
using CrmSync.Core.Target;
using static CrmSync.Core.Tests.TestData;

namespace CrmSync.Core.Tests;

public class SyncExecutorTests
{
    private readonly FakeClock _clock = new(Start.AddHours(1));
    private readonly SyncState _state = new();

    private static readonly AlphaContact[] Source =
    [
        Alpha("A-1", "Grace", "Hopper", "grace@example.com", phone: "555-010-9999"),
        Alpha("A-2", "Alan", "Turing", "alan@example.com"),
    ];

    private InMemoryTargetCrm Target(params BetaContact[] seed) => new(seed, _clock);

    [Fact]
    public void Applies_creates_and_updates_and_remembers_what_it_wrote()
    {
        var target = Target(Beta(5001, "Grace Hopper", "grace@example.com", externalRef: "A-1", phone: "5550100000"));
        var plan = SyncPlanner.Plan(Source, target.GetAll(), _state);

        var run = new SyncExecutor(target, _state, _clock).Apply(plan);

        Assert.Equal([ChangeAction.Update, ChangeAction.Create], run.Applied.Select(a => a.Action));
        Assert.Equal("5550109999", target.Find(5001)?.PhoneNumber);
        Assert.Equal("A-2", target.Find(5002)?.ExternalRef);
        Assert.All(target.GetAll(), c => Assert.Equal(c.ModifiedAt, _state.LastWritten[c.Id]));
    }

    [Fact]
    public void Planning_again_after_applying_finds_nothing_to_do()
    {
        var target = Target(Beta(5001, "Grace Hopper", "grace@example.com", externalRef: "A-1"));
        new SyncExecutor(target, _state, _clock).Apply(SyncPlanner.Plan(Source, target.GetAll(), _state));

        var replan = SyncPlanner.Plan(Source, target.GetAll(), _state);

        Assert.All(replan.Changes, c => Assert.Equal(ChangeAction.Unchanged, c.Action));
    }

    [Fact]
    public void Replaying_an_old_plan_does_not_create_duplicates()
    {
        var target = Target();
        var executor = new SyncExecutor(target, _state, _clock);
        var plan = SyncPlanner.Plan(Source, target.GetAll(), _state);
        executor.Apply(plan);

        var replay = executor.Apply(plan);

        Assert.Empty(replay.Applied);
        Assert.All(replay.Skipped, s => Assert.StartsWith("Stale", s.Reason));
        Assert.Equal(2, target.GetAll().Count);
    }

    [Fact]
    public void Skips_an_update_when_the_target_changed_after_planning()
    {
        var target = Target(Beta(5001, "Grace Hopper", "grace@example.com", externalRef: "A-1"));
        var plan = SyncPlanner.Plan(Source, target.GetAll(), _state);
        _clock.Advance(TimeSpan.FromMinutes(5));
        target.Update(5001, new BetaFields("Grace Hopper", "grace@example.com", null, "Edited meanwhile", "Prospect"), "A-1");

        var run = new SyncExecutor(target, _state, _clock).Apply(plan);

        Assert.Contains(run.Skipped, s => s.SourceId == "A-1" && s.Reason.StartsWith("Stale", StringComparison.Ordinal));
        Assert.Equal("Edited meanwhile", target.Find(5001)?.Account);
    }

    [Fact]
    public void Conflicts_are_skipped_unless_the_source_is_told_to_win()
    {
        _state.LastWritten[5001] = Start;
        var edited = Beta(5001, "Grace Hopper", "grace@example.com", externalRef: "A-1", account: "Edited in Beta", modifiedAt: Start.AddMinutes(30));

        var skippedTarget = Target(edited);
        var skippedRun = new SyncExecutor(skippedTarget, _state, _clock).Apply(SyncPlanner.Plan(Source[..1], skippedTarget.GetAll(), _state));
        Assert.Equal(ChangeAction.Conflict, Assert.Single(skippedRun.Skipped).Action);
        Assert.Equal("Edited in Beta", skippedTarget.Find(5001)?.Account);

        var forcedTarget = Target(edited);
        var forcedRun = new SyncExecutor(forcedTarget, _state, _clock).Apply(SyncPlanner.Plan(Source[..1], forcedTarget.GetAll(), _state), sourceWinsConflicts: true);
        Assert.Equal(ChangeAction.Conflict, Assert.Single(forcedRun.Applied).Action);
        Assert.Null(forcedTarget.Find(5001)?.Account);
    }

    [Fact]
    public void Invalid_records_are_reported_as_skipped()
    {
        var target = Target();
        var run = new SyncExecutor(target, _state, _clock).Apply(SyncPlanner.Plan([Alpha("A-9", "Bad", "Email", "nope")], [], _state));

        Assert.Equal(ChangeAction.Invalid, Assert.Single(run.Skipped).Action);
        Assert.Empty(target.GetAll());
    }
}
