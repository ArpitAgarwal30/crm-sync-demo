using System.Text.Json.Serialization;

namespace CrmSync.Core.Source;

/// <summary>A contact as the source system ("Alpha CRM", mock) exposes it: snake_case, split names.</summary>
public sealed record AlphaContact
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("email_address")]
    public string? EmailAddress { get; init; }

    [JsonPropertyName("phone")]
    public string? Phone { get; init; }

    [JsonPropertyName("company")]
    public string? Company { get; init; }

    /// <summary>"lead" or "customer".</summary>
    [JsonPropertyName("lifecycle")]
    public string? Lifecycle { get; init; }
}
