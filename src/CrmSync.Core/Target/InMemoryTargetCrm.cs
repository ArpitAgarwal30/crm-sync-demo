namespace CrmSync.Core.Target;

/// <summary>A mock target CRM. Like a real one, it assigns ids and stamps ModifiedAt on every write.</summary>
public sealed class InMemoryTargetCrm(IEnumerable<BetaContact> seed, TimeProvider clock) : ITargetCrm
{
    private readonly SortedDictionary<int, BetaContact> _contacts = new(seed.ToDictionary(c => c.Id));

    public IReadOnlyList<BetaContact> GetAll() => [.. _contacts.Values];

    public BetaContact? Find(int id) => _contacts.GetValueOrDefault(id);

    public BetaContact? FindByExternalRef(string externalRef) =>
        _contacts.Values.FirstOrDefault(c => c.ExternalRef == externalRef);

    public BetaContact Create(BetaFields fields, string externalRef)
    {
        var id = _contacts.Count == 0 ? 5001 : _contacts.Keys.Max() + 1;
        return _contacts[id] = Build(id, fields, externalRef);
    }

    public BetaContact Update(int id, BetaFields fields, string? externalRef)
    {
        if (!_contacts.ContainsKey(id))
        {
            throw new KeyNotFoundException($"Beta contact {id} does not exist.");
        }

        return _contacts[id] = Build(id, fields, externalRef);
    }

    public void Delete(int id) => _contacts.Remove(id);

    private BetaContact Build(int id, BetaFields fields, string? externalRef) => new()
    {
        Id = id,
        ExternalRef = externalRef,
        FullName = fields.FullName,
        Email = fields.Email,
        PhoneNumber = fields.PhoneNumber,
        Account = fields.Account,
        Stage = fields.Stage,
        ModifiedAt = clock.GetUtcNow(),
    };
}
