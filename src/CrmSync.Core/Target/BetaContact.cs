namespace CrmSync.Core.Target;

/// <summary>A contact as the target system ("Beta CRM", mock) stores it: PascalCase, one full-name field, numeric ids.</summary>
public sealed record BetaContact
{
    public required int Id { get; init; }

    /// <summary>The source record this contact is linked to; null for contacts created directly in Beta.</summary>
    public string? ExternalRef { get; init; }

    public required string FullName { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? Account { get; init; }

    /// <summary>"Prospect" or "Client".</summary>
    public required string Stage { get; init; }

    /// <summary>Set by the CRM on every write, like a real CRM's last-modified timestamp.</summary>
    public required DateTimeOffset ModifiedAt { get; init; }

    /// <summary>How every target CRM implementation builds a written record.</summary>
    public static BetaContact From(int id, BetaFields fields, string? externalRef, DateTimeOffset modifiedAt) => new()
    {
        Id = id,
        ExternalRef = externalRef,
        FullName = fields.FullName,
        Email = fields.Email,
        PhoneNumber = fields.PhoneNumber,
        Account = fields.Account,
        Stage = fields.Stage,
        ModifiedAt = modifiedAt,
    };
}

/// <summary>The fields the sync owns. Ids, links and timestamps are managed separately.</summary>
public sealed record BetaFields(string FullName, string? Email, string? PhoneNumber, string? Account, string Stage);
