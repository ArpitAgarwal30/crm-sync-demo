using CrmSync.Core.Source;
using CrmSync.Core.Target;

namespace CrmSync.Core.Tests;

/// <summary>A clock the tests control, so every ModifiedAt value is predictable.</summary>
internal sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

internal static class TestData
{
    public static readonly DateTimeOffset Start = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    public static AlphaContact Alpha(
        string id,
        string first,
        string last,
        string email,
        string? phone = null,
        string? company = null,
        string? lifecycle = "lead") => new()
    {
        Id = id,
        FirstName = first,
        LastName = last,
        EmailAddress = email,
        Phone = phone,
        Company = company,
        Lifecycle = lifecycle,
    };

    public static BetaContact Beta(
        int id,
        string fullName,
        string? email,
        string? externalRef = null,
        string? phone = null,
        string? account = null,
        string stage = "Prospect",
        DateTimeOffset? modifiedAt = null) => new()
    {
        Id = id,
        ExternalRef = externalRef,
        FullName = fullName,
        Email = email,
        PhoneNumber = phone,
        Account = account,
        Stage = stage,
        ModifiedAt = modifiedAt ?? Start,
    };
}
