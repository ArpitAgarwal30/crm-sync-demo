namespace CrmSync.Core.Target;

/// <summary>The operations the sync needs from the target CRM, the way a real CRM API would offer them.</summary>
public interface ITargetCrm
{
    IReadOnlyList<BetaContact> GetAll();

    BetaContact? Find(int id);

    BetaContact? FindByExternalRef(string externalRef);

    BetaContact Create(BetaFields fields, string externalRef);

    BetaContact Update(int id, BetaFields fields, string? externalRef);

    void Delete(int id);
}
