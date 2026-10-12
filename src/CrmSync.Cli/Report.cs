using CrmSync.Core.Sync;
using CrmSync.Core.Target;

namespace CrmSync.Cli;

/// <summary>Console output. Plain ASCII so it renders the same in every terminal.</summary>
internal static class Report
{
    public static void Plan(SyncPlan plan)
    {
        Console.WriteLine(
            $"Dry run: {plan.Count(ChangeAction.Create)} to create, {plan.Count(ChangeAction.Update)} to update, " +
            $"{plan.Count(ChangeAction.Unchanged)} unchanged, {plan.Count(ChangeAction.Conflict)} conflict(s), " +
            $"{plan.Count(ChangeAction.Invalid)} invalid. {plan.UnmanagedTargetIds.Count} Beta contact(s) not managed by the sync.");
        Console.WriteLine();

        foreach (var change in plan.Changes)
        {
            var target = change.TargetId is { } id ? $"-> #{id}" : "";
            Console.WriteLine($"  {change.Action.ToString().ToUpperInvariant(),-10}{change.SourceId,-8}{target,-9}{change.DisplayName}");
            if (change.Action != ChangeAction.Create)
            {
                PrintFields(change.Changes);
            }

            if (change.Reason is not null)
            {
                Console.WriteLine($"              ({change.Reason})");
            }
        }
    }

    public static void Run(SyncRun run)
    {
        Console.WriteLine();
        Console.WriteLine($"Applied run {run.RunId}: {run.Applied.Count} change(s) written, {run.Skipped.Count} skipped.");
        foreach (var skip in run.Skipped)
        {
            Console.WriteLine($"  SKIPPED   {skip.SourceId,-8}{skip.Reason}");
        }

        Console.WriteLine($"Undo with: crmsync rollback {run.RunId}");
    }

    public static void Rollback(RollbackResult result)
    {
        Console.WriteLine($"Rolled back run {result.Run.RunId}: {result.Restored.Count} contact(s) restored, {result.Refused.Count} left as is.");
        foreach (var refusal in result.Refused)
        {
            Console.WriteLine($"  LEFT AS IS  #{refusal.TargetId}  {refusal.Reason}");
        }
    }

    public static void Runs(IReadOnlyList<SyncRun> runs)
    {
        if (runs.Count == 0)
        {
            Console.WriteLine("No runs yet.");
            return;
        }

        foreach (var run in runs)
        {
            var status = run.RolledBackAt is null ? "active" : $"rolled back {run.RolledBackAt:u}";
            Console.WriteLine($"  {run.RunId}  {run.AppliedAt:u}  {run.Applied.Count} written, {run.Skipped.Count} skipped  [{status}]");
        }
    }

    private static void PrintFields(IReadOnlyList<FieldChange> changes)
    {
        foreach (var field in changes)
        {
            Console.WriteLine($"              {field.Describe()}");
        }
    }
}
