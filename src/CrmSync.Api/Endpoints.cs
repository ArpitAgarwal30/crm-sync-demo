using CrmSync.Api.Data;
using CrmSync.Api.Sync;
using CrmSync.Core.Model;
using CrmSync.Core.Source;
using CrmSync.Core.Sync;
using CrmSync.Core.Target;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace CrmSync.Api;

public static class Endpoints
{
    private static readonly string[] Stages = ["Prospect", "Client"];

    public static void MapSyncApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/source/contacts", (SyncDbContext db) =>
            TypedResults.Ok(db.SourceContacts.AsNoTracking().OrderBy(c => c.Id).ToList()));

        api.MapGet("/target/contacts", (SyncDbContext db) =>
            TypedResults.Ok(db.TargetContacts.AsNoTracking().OrderBy(c => c.Id).ToList()));

        api.MapPut("/target/contacts/{id:int}", EditTargetContact)
            .WithSummary("Edit a contact directly in Beta CRM, as one of its users would");

        api.MapPost("/plans", (SyncService sync) =>
        {
            var plan = sync.CreatePlan();
            return TypedResults.Created($"/api/plans/{plan.Id}", plan);
        }).WithSummary("Create a dry-run plan; nothing changes");

        api.MapGet("/plans/{planId}", Results<Ok<PlanResponse>, NotFound> (string planId, SyncService sync) =>
            sync.GetPlan(planId) is { } plan ? TypedResults.Ok(plan) : TypedResults.NotFound());

        api.MapPost("/plans/{planId}/apply", (string planId, SyncService sync, bool sourceWins = false) =>
            ToResult(sync.Apply(planId, sourceWins)))
            .WithSummary("Apply a stored plan; conflicts are skipped unless sourceWins=true");

        api.MapGet("/runs", (SyncService sync) => TypedResults.Ok(sync.GetRuns()));

        api.MapGet("/runs/{runId}", Results<Ok<SyncRun>, NotFound> (string runId, SyncService sync) =>
            sync.GetRun(runId) is { } run ? TypedResults.Ok(run) : TypedResults.NotFound());

        api.MapPost("/runs/{runId}/rollback", (string runId, SyncService sync) => ToResult(sync.Rollback(runId)))
            .WithSummary("Undo a run; contacts edited since are left as they are");

        api.MapGet("/audit", (SyncDbContext db, int take = 100) =>
            TypedResults.Ok(db.Audit.AsNoTracking().OrderByDescending(a => a.Id).Take(Math.Clamp(take, 1, 500)).ToList()))
            .WithSummary("Audit log, newest first");

        api.MapPost("/demo/reset", (DemoSeeder seeder) =>
        {
            seeder.Reset();
            return TypedResults.NoContent();
        }).WithSummary("Restore both mock CRMs and clear sync history");
    }

    private static Results<Ok<BetaContact>, NotFound, ValidationProblem> EditTargetContact(
        int id, EditContactRequest request, SyncDbContext db, TimeProvider clock)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            errors[nameof(request.FullName)] = ["Full name is required."];
        }

        if (!Stages.Contains(request.Stage))
        {
            errors[nameof(request.Stage)] = [$"Stage must be one of: {string.Join(", ", Stages)}."];
        }

        var email = Normalize.Email(request.Email);
        if (email is not null && !Normalize.IsValidEmail(email))
        {
            errors[nameof(request.Email)] = ["Email is not valid."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var existing = db.TargetContacts.AsNoTracking().SingleOrDefault(c => c.Id == id);
        if (existing is null)
        {
            return TypedResults.NotFound();
        }

        // Deliberately does not touch sync state: this is someone else's edit, which the next plan
        // must detect as a conflict rather than overwrite.
        var fields = new BetaFields(request.FullName.Trim(), email, Normalize.Phone(request.PhoneNumber), Normalize.Text(request.Account), request.Stage);
        return TypedResults.Ok(new EfTargetCrm(db, clock).Update(id, fields, existing.ExternalRef));
    }

    private static Results<Ok<T>, ProblemHttpResult> ToResult<T>(Outcome<T> outcome) => outcome.Failure switch
    {
        null => TypedResults.Ok(outcome.Value!),
        FailureKind.NotFound => TypedResults.Problem(outcome.Message, statusCode: StatusCodes.Status404NotFound),
        _ => TypedResults.Problem(outcome.Message, statusCode: StatusCodes.Status409Conflict),
    };
}
