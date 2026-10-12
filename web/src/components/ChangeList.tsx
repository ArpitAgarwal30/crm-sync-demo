import type { PlannedChange } from "../api/types";
import { showValue } from "../lib/format";
import { ActionBadge } from "./ActionBadge";

/** Every planned change, with a field-by-field before/after for anything that would be written. */
export function ChangeList({ changes }: { changes: PlannedChange[] }) {
  return (
    <ul className="divide-y divide-border rounded-lg border border-border">
      {changes.map((change) => (
        <li key={change.sourceId} className="px-4 py-3">
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <ActionBadge action={change.action} />
            <span className="font-medium">{change.displayName || change.sourceId}</span>
            <span className="font-mono text-xs text-muted">
              {change.sourceId}
              {change.targetId !== null && ` → #${change.targetId}`}
            </span>
          </div>
          {change.action !== "Create" && change.changes.length > 0 && (
            <table className="mt-2 text-sm">
              <caption className="sr-only">Field changes for {change.displayName}</caption>
              <thead className="sr-only">
                <tr>
                  <th scope="col">Field</th>
                  <th scope="col">Now in Beta</th>
                  <th scope="col">After sync</th>
                </tr>
              </thead>
              <tbody>
                {change.changes.map((field) => (
                  <tr key={field.field}>
                    <th scope="row" className="pr-3 text-left font-mono text-xs font-normal text-muted">
                      {field.field}
                    </th>
                    <td className={`pr-2 text-muted ${field.from === null ? "" : "line-through decoration-muted/60"}`}>{showValue(field.from)}</td>
                    <td>
                      <span aria-hidden="true" className="mr-2 text-muted">
                        →
                      </span>
                      {showValue(field.to)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {change.reason && <p className="mt-1.5 text-sm text-muted">{change.reason}</p>}
        </li>
      ))}
    </ul>
  );
}
