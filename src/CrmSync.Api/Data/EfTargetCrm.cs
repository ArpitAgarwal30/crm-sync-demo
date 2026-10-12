using CrmSync.Core.Target;
using Microsoft.EntityFrameworkCore;

namespace CrmSync.Api.Data;

/// <summary>
/// The mock Beta CRM backed by SQLite. Each write is saved immediately (inside the caller's
/// transaction) so later reads and id assignment within the same apply see it.
/// </summary>
public sealed class EfTargetCrm(SyncDbContext db, TimeProvider clock) : ITargetCrm
{
    public IReadOnlyList<BetaContact> GetAll() => db.TargetContacts.AsNoTracking().OrderBy(c => c.Id).ToList();

    public BetaContact? Find(int id) => db.TargetContacts.AsNoTracking().SingleOrDefault(c => c.Id == id);

    public BetaContact? FindByExternalRef(string externalRef) =>
        db.TargetContacts.AsNoTracking().FirstOrDefault(c => c.ExternalRef == externalRef);

    public BetaContact Create(BetaFields fields, string externalRef)
    {
        var id = (db.TargetContacts.Max(c => (int?)c.Id) ?? 5000) + 1;
        var contact = BetaContact.From(id, fields, externalRef, clock.GetUtcNow());
        Save(db.TargetContacts.Add(contact).Entity);
        return contact;
    }

    public BetaContact Update(int id, BetaFields fields, string? externalRef)
    {
        if (!db.TargetContacts.Any(c => c.Id == id))
        {
            throw new KeyNotFoundException($"Beta contact {id} does not exist.");
        }

        var contact = BetaContact.From(id, fields, externalRef, clock.GetUtcNow());
        Save(db.TargetContacts.Update(contact).Entity);
        return contact;
    }

    public void Delete(int id) => db.TargetContacts.Where(c => c.Id == id).ExecuteDelete();

    // Records are immutable, so detach after saving: the caller keeps its own copy (for example the
    // before-image used by rollback), and EF never tracks two instances of the same contact.
    private void Save(BetaContact contact)
    {
        db.SaveChanges();
        db.Entry(contact).State = EntityState.Detached;
    }
}
