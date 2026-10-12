import type { AuditEntry } from "../api/types";
import { formatDateTime } from "../lib/format";
import { Panel } from "./Panel";

export function AuditPanel({ entries }: { entries: AuditEntry[] }) {
  return (
    <Panel id="audit" title="Audit log" description="Append-only record of every write, skip and rollback, newest first.">
      {entries.length === 0 ? (
        <p className="text-sm text-muted">Nothing recorded yet.</p>
      ) : (
        <div className="relative max-h-96 overflow-auto">
          <table className="w-full text-sm">
            <caption className="sr-only">Audit log entries</caption>
            <thead className="sticky top-0 border-b border-border bg-surface">
              <tr>
                <th scope="col" className="px-2 py-2 text-left font-medium text-muted">When</th>
                <th scope="col" className="px-2 py-2 text-left font-medium text-muted">Event</th>
                <th scope="col" className="px-2 py-2 text-left font-medium text-muted">Record</th>
                <th scope="col" className="px-2 py-2 text-left font-medium text-muted">Detail</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {entries.map((entry) => (
                <tr key={entry.id} className="align-top">
                  <td className="whitespace-nowrap px-2 py-2 text-muted">
                    <time dateTime={entry.at}>{formatDateTime(entry.at)}</time>
                  </td>
                  <td className="whitespace-nowrap px-2 py-2 font-mono text-xs">{entry.event}</td>
                  <td className="whitespace-nowrap px-2 py-2 font-mono text-xs">
                    {entry.sourceId}
                    {entry.targetId !== null && ` → #${entry.targetId}`}
                  </td>
                  <td className="min-w-64 px-2 py-2">{entry.detail}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Panel>
  );
}
