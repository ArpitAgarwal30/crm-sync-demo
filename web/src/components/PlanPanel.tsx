import { useState } from "react";
import type { Plan, SyncRun } from "../api/types";
import { ChangeList } from "./ChangeList";
import { Panel, primaryButton, secondaryButton } from "./Panel";

interface PlanPanelProps {
  plan: Plan | null;
  lastRun: SyncRun | null;
  betaChangedSincePlan: boolean;
  busy: boolean;
  onCreatePlan: () => void;
  onApply: (sourceWins: boolean) => void;
}

export function PlanPanel({ plan, lastRun, betaChangedSincePlan, busy, onCreatePlan, onApply }: PlanPanelProps) {
  const [sourceWins, setSourceWins] = useState(false);

  const createButton = (
    <button type="button" className={plan ? secondaryButton : primaryButton} onClick={onCreatePlan} disabled={busy}>
      {plan ? "New dry run" : "Create dry run"}
    </button>
  );

  return (
    <Panel
      id="plan"
      title="Dry run"
      description="What a sync would change, computed without changing anything."
      actions={createButton}
    >
      {lastRun && !plan && (
        <div role="status" className="mb-4 rounded-lg border border-border bg-surface-2 p-4 text-sm">
          <p className="font-medium">
            Applied run <span className="font-mono">{lastRun.runId}</span>: {lastRun.applied.length} written,{" "}
            {lastRun.skipped.length} skipped.
          </p>
          {lastRun.skipped.length > 0 && (
            <ul className="mt-2 list-disc space-y-1 pl-5 text-muted">
              {lastRun.skipped.map((skip) => (
                <li key={skip.sourceId}>
                  <span className="font-mono text-xs">{skip.sourceId}</span>: {skip.reason}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {!plan ? (
        <p className="text-sm text-muted">Create a dry run to see every create, update, conflict and invalid record before anything is written.</p>
      ) : (
        <div className="space-y-4">
          <ul className="flex flex-wrap gap-2 text-sm" aria-label="Plan summary">
            <Count label="to create" value={plan.summary.create} />
            <Count label="to update" value={plan.summary.update} />
            <Count label="unchanged" value={plan.summary.unchanged} />
            <Count label={plan.summary.conflict === 1 ? "conflict" : "conflicts"} value={plan.summary.conflict} />
            <Count label="invalid" value={plan.summary.invalid} />
            <Count label="not managed" value={plan.summary.unmanaged} />
          </ul>

          <ChangeList changes={plan.changes} />

          {plan.unmanagedTargetIds.length > 0 && (
            <p className="text-sm text-muted">
              Beta contacts {plan.unmanagedTargetIds.map((id) => `#${id}`).join(", ")} have no source match. The sync never deletes,
              so they're left alone.
            </p>
          )}

          {betaChangedSincePlan && (
            <p className="rounded-lg border border-conflict/40 p-3 text-sm">
              Beta CRM changed after this dry run. Applying it anyway is safe: changes to records that moved since will be skipped
              as stale. Create a new dry run to see the current picture.
            </p>
          )}

          <div className="flex flex-wrap items-center justify-between gap-4 border-t border-border pt-4">
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={sourceWins} onChange={(e) => setSourceWins(e.target.checked)} className="size-4 accent-accent" />
              Let the source win conflicts (overwrite edits made in Beta)
            </label>
            <button type="button" className={primaryButton} onClick={() => onApply(sourceWins)} disabled={busy}>
              Apply this plan
            </button>
          </div>
        </div>
      )}
    </Panel>
  );
}

function Count({ label, value }: { label: string; value: number }) {
  return (
    <li className="rounded-md border border-border px-2.5 py-1">
      <span className="font-mono font-medium">{value}</span> <span className="text-muted">{label}</span>
    </li>
  );
}
