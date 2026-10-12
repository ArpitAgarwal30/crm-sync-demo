using System.Text.RegularExpressions;

namespace CrmSync.Core.Model;

/// <summary>Normalization applied before records are compared, so formatting noise never looks like a change.</summary>
public static partial class Normalize
{
    public static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string? Email(string? value) => Text(value)?.ToLowerInvariant();

    public static bool IsValidEmail(string? email) => email is not null && EmailPattern().IsMatch(email);

    /// <summary>Keeps digits and a leading "+": "(555) 010-2030" becomes "5550102030", "+1 555 0102" becomes "+15550102".</summary>
    public static string? Phone(string? value)
    {
        var text = Text(value);
        if (text is null)
        {
            return null;
        }

        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
        {
            return null;
        }

        return text.StartsWith('+') ? "+" + digits : digits;
    }

    // Deliberately simple: one "@", no spaces, a dot in the domain.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
