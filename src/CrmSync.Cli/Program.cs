using CrmSync.Cli;
using CrmSync.Core.Sync;
using CrmSync.Core.Target;

const string Usage = """
    Usage: crmsync <command> [--dir <demo folder>]

    Commands:
      plan                   Show what a sync would change, without changing anything
      apply [--source-wins]  Apply the sync; conflicts are skipped unless --source-wins
      rollback <run id>      Undo an applied run
      runs                   List applied runs
      audit                  Show the audit log
      reset                  Restore the demo's Beta CRM and clear sync history
    """;

var positional = new List<string>();
var dir = "demo";
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--dir" && i + 1 < args.Length)
    {
        dir = args[++i];
    }
    else if (!args[i].StartsWith("--", StringComparison.Ordinal))
    {
        positional.Add(args[i]);
    }
}

var workspace = new Workspace(dir);
if (positional.Count == 0)
{
    Console.WriteLine(Usage);
    return 1;
}

if (!workspace.Exists)
{
    Console.Error.WriteLine($"No demo data found in '{Path.GetFullPath(dir)}'. Run from the repository root or pass --dir.");
    return 1;
}

var clock = TimeProvider.System;

try
{
    switch (positional[0])
    {
        case "reset":
            workspace.Reset();
            Console.WriteLine("Demo reset: Beta CRM restored to its seed data and sync history cleared.");
            return 0;

        case "plan":
            workspace.EnsureInitialized();
            Report.Plan(SyncPlanner.Plan(workspace.LoadSource(), workspace.LoadTarget(), workspace.LoadState()));
            return 0;

        case "apply":
        {
            workspace.EnsureInitialized();
            var state = workspace.LoadState();
            var target = new InMemoryTargetCrm(workspace.LoadTarget(), clock);
            var plan = SyncPlanner.Plan(workspace.LoadSource(), target.GetAll(), state);
            Report.Plan(plan);

            var run = new SyncExecutor(target, state, clock).Apply(plan, sourceWinsConflicts: args.Contains("--source-wins"));
            workspace.Save(target, state);
            workspace.SaveRun(run);
            workspace.AppendAudit(AuditLog.ForRun(run));
            Report.Run(run);
            return 0;
        }

        case "rollback" when positional.Count == 2:
        {
            workspace.EnsureInitialized();
            var run = workspace.LoadRun(positional[1]);
            if (run is null)
            {
                Console.Error.WriteLine($"No run '{positional[1]}'. See 'crmsync runs'.");
                return 1;
            }

            var state = workspace.LoadState();
            var target = new InMemoryTargetCrm(workspace.LoadTarget(), clock);
            var result = new SyncExecutor(target, state, clock).Rollback(run);
            workspace.Save(target, state);
            workspace.SaveRun(result.Run);
            workspace.AppendAudit(AuditLog.ForRollback(result));
            Report.Rollback(result);
            return 0;
        }

        case "runs":
            workspace.EnsureInitialized();
            Report.Runs(workspace.LoadRuns());
            return 0;

        case "audit":
            workspace.EnsureInitialized();
            foreach (var line in workspace.ReadAudit())
            {
                Console.WriteLine(line);
            }

            return 0;

        default:
            Console.WriteLine(Usage);
            return 1;
    }
}
catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or InvalidDataException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
