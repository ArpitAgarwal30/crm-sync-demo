namespace CrmSync.Core.Model;

public enum LifecycleStage
{
    Lead,
    Customer,
}

/// <summary>
/// The canonical contact: what both CRMs mean, independent of either one's field names.
/// Values are already normalized (trimmed, lower-cased email, digits-only phone).
/// </summary>
public sealed record Contact(
    string SourceId,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? Company,
    LifecycleStage Stage);
