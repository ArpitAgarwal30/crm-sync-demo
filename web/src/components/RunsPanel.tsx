import type { RollbackResult, SyncRun } from "../api/types";
import { formatDateTime } from "../lib/format";
import { Panel, secondaryButton } from "./Panel";

interface RunsPanelProps {
  runs: SyncRun[];
  lastRollback: RollbackResult | null;
  busy: boolean;
  onRollback: (runId: string) => void;
}

export function RunsPanel({ runs, lastRollback, busy, onRollback }: RunsPanelProps) {
  return (
    <Panel id="runs" title="Applied runs" description="Each run keeps a before-image of every contact it changed, so it can be undone.">
      {lastRollback && (
        <div role="status" className="mb-4 rounded-lg border border-border bg-surface-2 p-4 text-sm">
          <p className="font-medium">
            Rolled back <span className="font-mono">{lastRollback.run.runId}</span>: {lastRollback.restored.length} restored,{" "}
            {lastRollback.refused.length} left as is.
          </p>
          {lastRollback.refused.length > 0 && (
            <ul className="mt-2 list-disc space-y-1 pl-5 text-muted">
              {lastRollback.refused.map((r) => (
                <li key={r.targetId}>
                  #{r.targetId}: {r.reason}
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {runs.length === 0 ? (
        <p className="text-sm text-muted">No runs yet.</p>
      ) : (
        <ul className="divide-y divide-border rounded-lg border border-border">
          {runs.map((run) => (
            <li key={run.runId} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm">
              <div className="min-w-0">
                <p className="font-mono text-xs">{run.runId}</p>
                <p className="mt-0.5 text-muted">
                  <time dateTime={run.appliedAt}>{formatDateTime(run.appliedAt)}</time> · {run.applied.length} written,{" "}
                  {run.skipped.length} skipped
                  {run.rolledBackAt && " · rolled back"}
                </p>
              </div>
              <button
                type="button"
                className={secondaryButton}
                onClick={() => onRollback(run.runId)}
                disabled={busy || run.rolledBackAt !== null}
              >
                {run.rolledBackAt ? "Rolled back" : "Roll back"}
                <span className="sr-only"> run {run.runId}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}
