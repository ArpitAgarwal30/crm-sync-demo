using CrmSync.Api.Data;
using CrmSync.Core.Sync;

namespace CrmSync.Api.Sync;

public sealed record PlanSummary(int Create, int Update, int Unchanged, int Conflict, int Invalid, int Unmanaged);

public sealed record PlanResponse(
    string Id,
    DateTimeOffset CreatedAt,
    string? AppliedRunId,
    PlanSummary Summary,
    IReadOnlyList<PlannedChange> Changes,
    IReadOnlyList<int> UnmanagedTargetIds)
{
    public static PlanResponse From(PlanRecord record, SyncPlan plan) => new(
        record.Id,
        record.CreatedAt,
        record.AppliedRunId,
        new PlanSummary(
            plan.Count(ChangeAction.Create),
            plan.Count(ChangeAction.Update),
            plan.Count(ChangeAction.Unchanged),
            plan.Count(ChangeAction.Conflict),
            plan.Count(ChangeAction.Invalid),
            plan.UnmanagedTargetIds.Count),
        plan.Changes,
        plan.UnmanagedTargetIds);
}

/// <summary>An edit made directly in the Beta CRM, the way one of its users would. Used to demonstrate conflicts.</summary>
public sealed record EditContactRequest(string FullName, string? Email, string? PhoneNumber, string? Account, string Stage);
