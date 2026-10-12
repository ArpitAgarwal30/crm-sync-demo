using CrmSync.Core.Model;

namespace CrmSync.Core.Source;

public sealed record MappingResult(Contact? Contact, string? Error)
{
    public static MappingResult Ok(Contact contact) => new(contact, null);

    public static MappingResult Fail(string error) => new(null, error);
}

/// <summary>Maps and validates source records. Invalid records are reported, never guessed at.</summary>
public static class AlphaAdapter
{
    public static MappingResult ToCanonical(AlphaContact record)
    {
        var email = Normalize.Email(record.EmailAddress);
        if (!Normalize.IsValidEmail(email))
        {
            return MappingResult.Fail($"Invalid email '{record.EmailAddress}'");
        }

        var firstName = Normalize.Text(record.FirstName) ?? "";
        var lastName = Normalize.Text(record.LastName) ?? "";
        if (firstName.Length == 0 && lastName.Length == 0)
        {
            return MappingResult.Fail("Missing name");
        }

        LifecycleStage? stage = Normalize.Text(record.Lifecycle)?.ToLowerInvariant() switch
        {
            "lead" or null => LifecycleStage.Lead,
            "customer" => LifecycleStage.Customer,
            _ => null,
        };
        if (stage is null)
        {
            return MappingResult.Fail($"Unknown lifecycle '{record.Lifecycle}'");
        }

        return MappingResult.Ok(new Contact(
            record.Id,
            firstName,
            lastName,
            email!,
            Normalize.Phone(record.Phone),
            Normalize.Text(record.Company),
            stage.Value));
    }
}
