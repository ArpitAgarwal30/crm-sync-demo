using CrmSync.Core.Model;

namespace CrmSync.Core.Target;

public sealed record FieldChange(string Field, string? From, string? To)
{
    /// <summary>"PhoneNumber: (empty) -> '5550101969'": one format for reports and the audit log.</summary>
    public string Describe() => $"{Field}: {Show(From)} -> {Show(To)}";

    private static string Show(string? value) => value is null ? "(empty)" : $"'{value}'";
}

public static class BetaAdapter
{
    public static BetaFields ToFields(Contact contact) => new(
        FullName: $"{contact.FirstName} {contact.LastName}".Trim(),
        Email: contact.Email,
        PhoneNumber: contact.Phone,
        Account: contact.Company,
        Stage: contact.Stage == LifecycleStage.Customer ? "Client" : "Prospect");

    public static BetaFields FieldsOf(BetaContact contact) =>
        new(contact.FullName, contact.Email, contact.PhoneNumber, contact.Account, contact.Stage);

    /// <summary>
    /// Compares in the target's own shape. Comparing canonical models instead would be lossy:
    /// "Mary Ann Smith" can't be split back into first and last name reliably, so it would
    /// look changed on every run.
    /// </summary>
    public static IReadOnlyList<FieldChange> Diff(BetaFields? current, BetaFields desired)
    {
        var changes = new List<FieldChange>();
        Compare(nameof(BetaFields.FullName), current?.FullName, desired.FullName);
        Compare(nameof(BetaFields.Email), current?.Email, desired.Email);
        Compare(nameof(BetaFields.PhoneNumber), current?.PhoneNumber, desired.PhoneNumber);
        Compare(nameof(BetaFields.Account), current?.Account, desired.Account);
        Compare(nameof(BetaFields.Stage), current?.Stage, desired.Stage);
        return changes;

        void Compare(string field, string? from, string? to)
        {
            if (!string.Equals(from, to, StringComparison.Ordinal))
            {
                changes.Add(new FieldChange(field, from, to));
            }
        }
    }
}
