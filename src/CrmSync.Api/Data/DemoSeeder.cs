using System.Text.Json;
using CrmSync.Core.Source;
using CrmSync.Core.Sync;
using CrmSync.Core.Target;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmSync.Api.Data;

public sealed class DemoOptions
{
    /// <summary>Folder with the demo seed files, relative to the API's content root.</summary>
    public string DataPath { get; set; } = "../../demo";
}

/// <summary>Loads the same seed files the CLI uses, so both tell the same story.</summary>
public sealed class DemoSeeder(SyncDbContext db, IOptions<DemoOptions> options, IHostEnvironment environment)
{
    public void EnsureSeeded()
    {
        db.Database.EnsureCreated();
        if (!db.SourceContacts.Any())
        {
            Reset();
        }
    }

    public void Reset()
    {
        using var transaction = db.Database.BeginTransaction();

        db.Audit.ExecuteDelete();
        db.Runs.ExecuteDelete();
        db.Plans.ExecuteDelete();
        db.SyncState.ExecuteDelete();
        db.TargetContacts.ExecuteDelete();
        db.SourceContacts.ExecuteDelete();

        db.SourceContacts.AddRange(Read<List<AlphaContact>>("source-crm.json"));
        db.TargetContacts.AddRange(Read<List<BetaContact>>("target-crm.seed.json"));
        db.SyncState.AddRange(Read<SyncState>("sync-state.seed.json").LastWritten
            .Select(entry => new SyncStateRow { TargetId = entry.Key, LastWritten = entry.Value }));

        db.SaveChanges();
        db.ChangeTracker.Clear();
        transaction.Commit();
    }

    private T Read<T>(string file)
    {
        var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.DataPath, file));
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Demo file {path} is empty.");
    }
}
