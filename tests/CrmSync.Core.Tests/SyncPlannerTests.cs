using CrmSync.Core.Sync;
using static CrmSync.Core.Tests.TestData;

namespace CrmSync.Core.Tests;

public class SyncPlannerTests
{
    [Fact]
    public void Creates_a_contact_that_does_not_exist_in_the_target()
    {
        var plan = SyncPlanner.Plan([Alpha("A-1", "Alan", "Turing", "alan@example.com")], [], new SyncState());

        var change = Assert.Single(plan.Changes);
        Assert.Equal(ChangeAction.Create, change.Action);
        Assert.Equal("Alan Turing", change.Desired?.FullName);
    }

    [Fact]
    public void Leaves_a_linked_contact_alone_when_nothing_changed()
    {
        var plan = SyncPlanner.Plan(
            [Alpha("A-1", "Ada", "Lovelace", "ada@example.com")],
            [Beta(5001, "Ada Lovelace", "ada@example.com", externalRef: "A-1")],
            new SyncState());

        Assert.Equal(ChangeAction.Unchanged, Assert.Single(plan.Changes).Action);
    }

    [Fact]
    public void Multi_word_names_do_not_look_changed_on_every_run()
    {
        var plan = SyncPlanner.Plan(
            [Alpha("A-1", "Mary Ann", "Smith", "mary@example.com")],
            [Beta(5001, "Mary Ann Smith", "mary@example.com", externalRef: "A-1")],
            new SyncState());

        Assert.Equal(ChangeAction.Unchanged, Assert.Single(plan.Changes).Action);
    }

    [Fact]
    public void Updates_only_the_fields_that_differ()
    {
        var plan = SyncPlanner.Plan(
            [Alpha("A-1", "Grace", "Hopper", "grace@example.com", phone: "555-010-9999")],
            [Beta(5001, "Grace Hopper", "grace@example.com", externalRef: "A-1", phone: "5550100000")],
            new SyncState());

        var change = Assert.Single(plan.Changes);
        Assert.Equal(ChangeAction.Update, change.Action);
        Assert.Equal("PhoneNumber", Assert.Single(change.Changes).Field);
        Assert.Equal(Start, change.ExpectedTargetVersion);
    }

    [Fact]
    public void Matches_an_unlinked_contact_by_email_and_links_it()
    {
        var plan = SyncPlanner.Plan(
            [Alpha("A-1", "Katherine", "Johnson", "KJ@Example.com")],
            [Beta(5001, "Katherine Johnson", "kj@example.com")],
            new SyncState());

        var change = Assert.Single(plan.Changes);
        Assert.Equal(ChangeAction.Update, change.Action);
        Assert.Equal(5001, change.TargetId);
        Assert.Empty(change.Changes);
        Assert.Contains("linked", change.Reason);
    }

    [Fact]
    public void Flags_a_conflict_when_the_target_was_edited_since_the_last_sync()
    {
        var state = new SyncState();
        state.LastWritten[5001] = Start;
        var editedLater = Beta(5001, "Margaret Hamilton", "margaret@example.com", externalRef: "A-1", account: "Edited in Beta", modifiedAt: Start.AddDays(1));

        var plan = SyncPlanner.Plan([Alpha("A-1", "Margaret", "Hamilton", "margaret@example.com", company: "Source Co")], [editedLater], state);

        Assert.Equal(ChangeAction.Conflict, Assert.Single(plan.Changes).Action);
    }

    [Fact]
    public void Is_not_a_conflict_when_the_target_is_as_the_sync_last_wrote_it()
    {
        var state = new SyncState();
        state.LastWritten[5001] = Start;

        var plan = SyncPlanner.Plan(
            [Alpha("A-1", "Margaret", "Hamilton", "margaret@example.com", company: "New Co")],
            [Beta(5001, "Margaret Hamilton", "margaret@example.com", externalRef: "A-1", account: "Old Co")],
            state);

        Assert.Equal(ChangeAction.Update, Assert.Single(plan.Changes).Action);
    }

    [Fact]
    public void Reports_invalid_source_records_instead_of_guessing()
    {
        var plan = SyncPlanner.Plan([Alpha("A-1", "Linus", "T", "linus-at-example")], [], new SyncState());

        var change = Assert.Single(plan.Changes);
        Assert.Equal(ChangeAction.Invalid, change.Action);
        Assert.StartsWith("Invalid email", change.Reason);
    }

    [Fact]
    public void Never_plans_to_touch_target_contacts_without_a_source_match()
    {
        var plan = SyncPlanner.Plan([], [Beta(5009, "Joan Clarke", "joan@example.com")], new SyncState());

        Assert.Empty(plan.Changes);
        Assert.Equal([5009], plan.UnmanagedTargetIds);
    }

    [Fact]
    public void Two_source_records_cannot_claim_the_same_target_contact()
    {
        var plan = SyncPlanner.Plan(
            [Alpha("A-1", "Ada", "Lovelace", "ada@example.com"), Alpha("A-2", "Ada", "L", "ADA@example.com")],
            [Beta(5001, "Ada Lovelace", "ada@example.com")],
            new SyncState());

        Assert.Equal([ChangeAction.Update, ChangeAction.Invalid], plan.Changes.Select(c => c.Action));
    }
}
