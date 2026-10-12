using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrmSync.Api.Data;
using CrmSync.Api.Sync;
using CrmSync.Core.Sync;
using CrmSync.Core.Target;
using Microsoft.AspNetCore.Mvc;

namespace CrmSync.Api.Tests;

/// <summary>End-to-end tests over HTTP: real routing, JSON, EF Core and SQLite. Each test starts from the demo seed.</summary>
public sealed class SyncApiTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private readonly HttpClient _client = factory.CreateClient();

    public async Task InitializeAsync() => (await _client.PostAsync("/api/demo/reset", null)).EnsureSuccessStatusCode();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_plan_covers_every_case_in_the_demo_data()
    {
        var plan = await CreatePlan();

        Assert.Equal(new PlanSummary(Create: 1, Update: 2, Unchanged: 2, Conflict: 1, Invalid: 2, Unmanaged: 1), plan.Summary);
        Assert.Equal([5006], plan.UnmanagedTargetIds);
    }

    [Fact]
    public async Task Applying_a_plan_saves_the_changes_the_run_and_the_audit_entries()
    {
        var run = await ApplyOk((await CreatePlan()).Id);

        Assert.Equal(3, run.Applied.Count);
        Assert.Equal(3, run.Skipped.Count);
        Assert.Contains(await Target(), c => c.ExternalRef == "A-1003");
        Assert.Single(await Get<List<SyncRun>>("/api/runs"));
        Assert.Equal(6, (await Get<List<AuditRow>>("/api/audit")).Count);
    }

    [Fact]
    public async Task A_plan_can_only_be_applied_once()
    {
        var planId = (await CreatePlan()).Id;
        await ApplyOk(planId);

        var second = await _client.PostAsync($"/api/plans/{planId}/apply", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Contains("already applied", (await second.Content.ReadFromJsonAsync<ProblemDetails>(Json))?.Detail);
    }

    [Fact]
    public async Task A_Beta_edit_made_after_planning_is_not_overwritten()
    {
        var planId = (await CreatePlan()).Id;
        await EditDaniel("Edited by a Beta user");

        var run = await ApplyOk(planId);

        Assert.Contains(run.Skipped, s => s.SourceId == "A-1002" && s.Reason.StartsWith("Stale", StringComparison.Ordinal));
        Assert.Equal("Edited by a Beta user", (await Target()).Single(c => c.Id == 5002).Account);
    }

    [Fact]
    public async Task A_Beta_edit_after_a_sync_is_reported_as_a_conflict_next_time()
    {
        await ApplyOk((await CreatePlan()).Id);
        await EditDaniel("Edited by a Beta user");

        var next = await CreatePlan();

        Assert.Equal(ChangeAction.Conflict, next.Changes.Single(c => c.SourceId == "A-1002").Action);
    }

    [Fact]
    public async Task Rolling_back_restores_the_target_and_cannot_be_repeated()
    {
        var run = await ApplyOk((await CreatePlan()).Id);

        var rollback = await _client.PostAsync($"/api/runs/{run.RunId}/rollback", null);
        var result = await rollback.Content.ReadFromJsonAsync<RollbackResult>(Json);
        var again = await _client.PostAsync($"/api/runs/{run.RunId}/rollback", null);

        Assert.Equal(HttpStatusCode.OK, rollback.StatusCode);
        Assert.Equal(3, result?.Restored.Count);
        var target = await Target();
        Assert.DoesNotContain(target, c => c.ExternalRef == "A-1003");
        Assert.Equal("5550100000", target.Single(c => c.Id == 5002).PhoneNumber);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Unknown_plans_and_runs_return_not_found()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/api/plans/plan-missing")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync("/api/plans/plan-missing/apply", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsync("/api/runs/run-missing/rollback", null)).StatusCode);
    }

    [Fact]
    public async Task Invalid_edits_are_rejected_with_field_level_errors()
    {
        var response = await _client.PutAsJsonAsync("/api/target/contacts/5002", new EditContactRequest(" ", "not-an-email", null, null, "Partner"), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(Json);
        Assert.Equal(["Email", "FullName", "Stage"], problem?.Errors.Keys.Order());
    }

    [Fact]
    public async Task Editing_a_missing_contact_returns_not_found()
    {
        var response = await _client.PutAsJsonAsync("/api/target/contacts/9999", new EditContactRequest("Someone", null, null, null, "Prospect"), Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_check_responds() =>
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/health")).StatusCode);

    private async Task<PlanResponse> CreatePlan()
    {
        var response = await _client.PostAsync("/api/plans", null);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PlanResponse>(Json))!;
    }

    private async Task<SyncRun> ApplyOk(string planId)
    {
        var response = await _client.PostAsync($"/api/plans/{planId}/apply", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SyncRun>(Json))!;
    }

    private async Task EditDaniel(string account)
    {
        var response = await _client.PutAsJsonAsync(
            "/api/target/contacts/5002",
            new EditContactRequest("Daniel Okafor", "daniel@brightwater-health.example", "5550100000", account, "Client"),
            Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<List<BetaContact>> Target() => Get<List<BetaContact>>("/api/target/contacts");

    private async Task<T> Get<T>(string url) => (await _client.GetFromJsonAsync<T>(url, Json))!;
}
