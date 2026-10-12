using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace CrmSync.Api.Tests;

/// <summary>Hosts the real API in memory against its own throwaway SQLite file and the repo's demo data.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"crmsync-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Path"] = _databasePath,
            ["Demo:DataPath"] = Path.Combine(RepositoryRoot(), "demo"),
        }));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // Pooled connections keep the file open on Windows.
            SqliteConnection.ClearAllPools();
            File.Delete(_databasePath);
        }
    }

    // Starts from this source file's location (fixed at compile time), so it works whatever the build output folder is.
    private static string RepositoryRoot([CallerFilePath] string sourceFile = "")
    {
        var directory = new DirectoryInfo(Path.GetDirectoryName(sourceFile)!);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrmSync.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find the repository root (CrmSync.slnx).");
    }
}
