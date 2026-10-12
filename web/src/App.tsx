import { useEffect, useState } from "react";
import { api } from "./api/client";
import type { AuditEntry, ContactEdit, Plan, RollbackResult, SourceContact, SyncRun, TargetContact } from "./api/types";
import { AuditPanel } from "./components/AuditPanel";
import { SourceTable, TargetTable } from "./components/ContactTables";
import { EditContactDialog } from "./components/EditContactDialog";
import { Panel, secondaryButton } from "./components/Panel";
import { PlanPanel } from "./components/PlanPanel";
import { RunsPanel } from "./components/RunsPanel";

interface Snapshot {
  source: SourceContact[];
  target: TargetContact[];
  runs: SyncRun[];
  audit: AuditEntry[];
}

const errorMessage = (error: unknown) => (error instanceof Error ? error.message : "Something went wrong.");

async function loadSnapshot(): Promise<Snapshot> {
  const [source, target, runs, audit] = await Promise.all([api.sourceContacts(), api.targetContacts(), api.runs(), api.audit()]);
  return { source, target, runs, audit };
}

export function App() {
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [plan, setPlan] = useState<Plan | null>(null);
  const [lastRun, setLastRun] = useState<SyncRun | null>(null);
  const [lastRollback, setLastRollback] = useState<RollbackResult | null>(null);
  const [editing, setEditing] = useState<TargetContact | null>(null);
  const [betaChangedSincePlan, setBetaChangedSincePlan] = useState(false);
  const [busy, setBusy] = useState(false);
  const [status, setStatus] = useState("");
  const [error, setError] = useState<string | null>(null);

  const refresh = async () => setSnapshot(await loadSnapshot());

  // Initial load. The flag ignores a response that arrives after unmount (or after StrictMode's extra mount).
  useEffect(() => {
    let active = true;
    loadSnapshot().then(
      (loaded) => active && setSnapshot(loaded),
      (e: unknown) => active && setError(errorMessage(e)),
    );
    return () => {
      active = false;
    };
  }, []);

  // Runs an action, refreshes both CRMs and the history, and announces the result.
  async function perform(action: () => Promise<string>) {
    setBusy(true);
    setError(null);
    try {
      const message = await action();
      await refresh();
      setStatus(message);
    } catch (e) {
      setError(errorMessage(e));
    } finally {
      setBusy(false);
    }
  }

  const createPlan = () =>
    perform(async () => {
      const created = await api.createPlan();
      setPlan(created);
      setBetaChangedSincePlan(false);
      setLastRun(null);
      const s = created.summary;
      return `Dry run ready: ${s.create} to create, ${s.update} to update, ${s.conflict} conflict(s), ${s.invalid} invalid.`;
    });

  const applyPlan = (sourceWins: boolean) =>
    perform(async () => {
      if (!plan) return "";
      const run = await api.applyPlan(plan.id, sourceWins);
      setPlan(null);
      setLastRun(run);
      setLastRollback(null);
      return `Applied run ${run.runId}: ${run.applied.length} written, ${run.skipped.length} skipped.`;
    });

  const rollback = (runId: string) =>
    perform(async () => {
      const result = await api.rollback(runId);
      setLastRollback(result);
      if (plan) setBetaChangedSincePlan(true);
      return `Rolled back run ${runId}: ${result.restored.length} restored, ${result.refused.length} left as is.`;
    });

  const reset = () =>
    perform(async () => {
      await api.reset();
      setPlan(null);
      setLastRun(null);
      setLastRollback(null);
      setBetaChangedSincePlan(false);
      return "Demo reset to its starting data.";
    });

  // Errors are thrown back to the dialog so it can show field-level messages.
  const saveEdit = async (edit: ContactEdit) => {
    if (!editing) return;
    const saved = await api.editTargetContact(editing.id, edit);
    setEditing(null);
    if (plan) setBetaChangedSincePlan(true);
    await refresh();
    setStatus(`Saved contact #${saved.id} in Beta CRM, as a Beta user would.`);
  };

  return (
    <div className="mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="max-w-3xl">
          <p className="font-mono text-xs tracking-wide text-accent uppercase">Independent demo · mock data, fictional contacts</p>
          <h1 className="mt-2 text-3xl font-semibold tracking-tight">CRM Sync Demo</h1>
          <p className="mt-2 text-pretty text-muted">
            Syncs contacts from Alpha CRM into Beta CRM safely: preview every change, catch edits made in Beta as conflicts instead
            of overwriting them, never delete, and undo any run.
          </p>
        </div>
        <button type="button" className={secondaryButton} onClick={reset} disabled={busy}>
          Reset demo
        </button>
      </header>

      <ol className="mt-6 grid gap-2 text-sm text-muted sm:grid-cols-2 lg:grid-cols-4">
        {[
          "Create a dry run and review every change.",
          "Apply it. Conflicts and invalid records are skipped.",
          "Edit a Beta contact, then dry-run again to see a conflict.",
          "Roll back any run to its before-images.",
        ].map((step, i) => (
          <li key={step} className="flex gap-2 rounded-lg border border-border px-3 py-2">
            <span className="font-mono text-accent">{i + 1}</span>
            {step}
          </li>
        ))}
      </ol>

      <p role="status" className="sr-only">
        {status}
      </p>
      {error && (
        <p role="alert" className="mt-6 rounded-lg border border-invalid/50 px-4 py-3 text-sm text-invalid">
          {error}
        </p>
      )}

      {!snapshot ? (
        !error && <p className="mt-8 text-muted">Loading…</p>
      ) : (
        <main className="mt-8 space-y-6">
          <div className="grid gap-6 xl:grid-cols-2">
            <Panel id="source" title="Alpha CRM (source)" description="snake_case fields, split names, lead / customer.">
              <SourceTable contacts={snapshot.source} />
            </Panel>
            <Panel id="target" title="Beta CRM (target)" description="One FullName, Prospect / Client, linked back to the source.">
              <TargetTable contacts={snapshot.target} onEdit={setEditing} disabled={busy} />
            </Panel>
          </div>

          <PlanPanel
            plan={plan}
            lastRun={lastRun}
            betaChangedSincePlan={betaChangedSincePlan}
            busy={busy}
            onCreatePlan={createPlan}
            onApply={applyPlan}
          />

          <RunsPanel runs={snapshot.runs} lastRollback={lastRollback} busy={busy} onRollback={rollback} />
          {/* Full width: the Detail column needs the room. */}
          <AuditPanel entries={snapshot.audit} />
        </main>
      )}

      <EditContactDialog contact={editing} onClose={() => setEditing(null)} onSave={saveEdit} />

      <footer className="mt-12 border-t border-border pt-6 text-sm text-muted">
        Built by{" "}
        <a href="https://arpitagarwal.vercel.app" className="text-accent hover:underline">
          Arpit Agarwal
        </a>{" "}
        with C#, ASP.NET Core, EF Core, React and TypeScript.
      </footer>
    </div>
  );
}
