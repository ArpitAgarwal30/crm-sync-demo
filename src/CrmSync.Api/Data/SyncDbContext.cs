using CrmSync.Core.Source;
using CrmSync.Core.Target;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CrmSync.Api.Data;

/// <summary>
/// One SQLite database holds both mock CRMs and the sync's own records, so a whole apply or
/// rollback can run inside a single transaction.
/// </summary>
public sealed class SyncDbContext(DbContextOptions<SyncDbContext> options) : DbContext(options)
{
    public DbSet<AlphaContact> SourceContacts => Set<AlphaContact>();
    public DbSet<BetaContact> TargetContacts => Set<BetaContact>();
    public DbSet<SyncStateRow> SyncState => Set<SyncStateRow>();
    public DbSet<PlanRecord> Plans => Set<PlanRecord>();
    public DbSet<RunRecord> Runs => Set<RunRecord>();
    public DbSet<AuditRow> Audit => Set<AuditRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Conflict and rollback checks compare timestamps for exact equality, so storage must be
        // lossless. EF's built-in binary converter truncates precision; UTC ticks do not, and sort correctly.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AlphaContact>().ToTable("SourceContacts").HasKey(c => c.Id);

        modelBuilder.Entity<BetaContact>(entity =>
        {
            entity.ToTable("TargetContacts");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).ValueGeneratedNever();
            entity.HasIndex(c => c.ExternalRef);
        });

        modelBuilder.Entity<SyncStateRow>(entity =>
        {
            entity.HasKey(s => s.TargetId);
            entity.Property(s => s.TargetId).ValueGeneratedNever();
        });

        modelBuilder.Entity<PlanRecord>().HasKey(p => p.Id);
        modelBuilder.Entity<RunRecord>().HasKey(r => r.RunId);
        modelBuilder.Entity<AuditRow>().HasKey(a => a.Id);
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        value => value.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
}

public sealed class SyncStateRow
{
    public int TargetId { get; set; }

    public DateTimeOffset LastWritten { get; set; }
}

/// <summary>A stored dry run, so the UI can show a plan and apply exactly that plan later.</summary>
public sealed class PlanRecord
{
    public required string Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? AppliedRunId { get; set; }

    public required string PlanJson { get; set; }
}

public sealed class RunRecord
{
    public required string RunId { get; set; }

    public required string PlanId { get; set; }

    public DateTimeOffset AppliedAt { get; set; }

    public DateTimeOffset? RolledBackAt { get; set; }

    public required string RunJson { get; set; }
}

public sealed class AuditRow
{
    public long Id { get; set; }

    public DateTimeOffset At { get; set; }

    public required string RunId { get; set; }

    public required string Event { get; set; }

    public required string SourceId { get; set; }

    public int? TargetId { get; set; }

    public required string Detail { get; set; }
}
