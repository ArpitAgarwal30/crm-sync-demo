using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using CrmSync.Core.Source;
using CrmSync.Core.Sync;
using CrmSync.Core.Target;

namespace CrmSync.Cli;

/// <summary>
/// The demo's files. Seeds are read-only; everything the sync changes lives in .work/ so
/// "reset" can always return to a known starting point.
/// </summary>
internal sealed partial class Workspace(string root)
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    // The audit log is a local text file, never embedded in HTML, so skip escaping quotes and arrows.
    private static readonly JsonSerializerOptions Compact = new()
    {
        Converters = { new JsonStringEnumConverter() },
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private string WorkDir => Path.Combine(root, ".work");
    private string TargetFile => Path.Combine(WorkDir, "target-crm.json");
    private string StateFile => Path.Combine(WorkDir, "sync-state.json");
    private string RunsDir => Path.Combine(WorkDir, "runs");
    private string AuditFile => Path.Combine(WorkDir, "audit.jsonl");

    public bool Exists => File.Exists(Path.Combine(root, "source-crm.json"));

    public void Reset()
    {
        if (Directory.Exists(WorkDir))
        {
            Directory.Delete(WorkDir, recursive: true);
        }

        Directory.CreateDirectory(RunsDir);
        File.Copy(Path.Combine(root, "target-crm.seed.json"), TargetFile);
        File.Copy(Path.Combine(root, "sync-state.seed.json"), StateFile);
    }

    public void EnsureInitialized()
    {
        if (!File.Exists(TargetFile))
        {
            Reset();
        }
    }

    public IReadOnlyList<AlphaContact> LoadSource() => Read<List<AlphaContact>>(Path.Combine(root, "source-crm.json"));

    public IReadOnlyList<BetaContact> LoadTarget() => Read<List<BetaContact>>(TargetFile);

    public SyncState LoadState() => Read<SyncState>(StateFile);

    public void Save(ITargetCrm target, SyncState state)
    {
        WriteAtomically(TargetFile, target.GetAll());
        WriteAtomically(StateFile, state);
    }

    public void SaveRun(SyncRun run) => WriteAtomically(RunFile(run.RunId), run);

    public SyncRun? LoadRun(string runId) => File.Exists(RunFile(runId)) ? Read<SyncRun>(RunFile(runId)) : null;

    public IReadOnlyList<SyncRun> LoadRuns() =>
        [.. Directory.EnumerateFiles(RunsDir, "*.json").Select(Read<SyncRun>).OrderBy(r => r.AppliedAt)];

    public void AppendAudit(IEnumerable<AuditEntry> entries) =>
        File.AppendAllLines(AuditFile, entries.Select(e => JsonSerializer.Serialize(e, Compact)));

    public IReadOnlyList<string> ReadAudit() => File.Exists(AuditFile) ? File.ReadAllLines(AuditFile) : [];

    private string RunFile(string runId)
    {
        // Run ids come from the command line; never let one escape the runs folder.
        if (!RunIdPattern().IsMatch(runId))
        {
            throw new ArgumentException($"'{runId}' is not a valid run id.");
        }

        return Path.Combine(RunsDir, $"{runId}.json");
    }

    private static T Read<T>(string path) =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), Pretty)
        ?? throw new InvalidDataException($"{path} is empty or not valid JSON.");

    // Write to a temp file and rename, so an interrupted write never leaves a half-written file.
    private static void WriteAtomically<T>(string path, T value)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Pretty));
        File.Move(temp, path, overwrite: true);
    }

    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex RunIdPattern();
}
