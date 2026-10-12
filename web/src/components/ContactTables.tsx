import type { SourceContact, TargetContact } from "../api/types";
import { formatDateTime } from "../lib/format";
import { secondaryButton } from "./Panel";

// Scroll containers below are `relative` so visually hidden (absolutely positioned) labels inside
// the tables are clipped by them instead of widening the whole page.
const th = "whitespace-nowrap px-3 py-2 text-left font-medium text-muted";
const td = "whitespace-nowrap px-3 py-2";

function Empty() {
  return <span className="text-muted">(empty)</span>;
}

export function SourceTable({ contacts }: { contacts: SourceContact[] }) {
  return (
    <div className="relative overflow-x-auto">
      <table className="w-full text-sm">
        <caption className="sr-only">Contacts in Alpha CRM, the source system</caption>
        <thead className="border-b border-border">
          <tr>
            <th scope="col" className={th}>id</th>
            <th scope="col" className={th}>first_name / last_name</th>
            <th scope="col" className={th}>email_address</th>
            <th scope="col" className={th}>phone</th>
            <th scope="col" className={th}>lifecycle</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {contacts.map((c) => (
            <tr key={c.id}>
              <td className={`${td} font-mono text-xs`}>{c.id}</td>
              <td className={td}>
                {c.first_name} {c.last_name}
              </td>
              <td className={td}>{c.email_address?.trim() || <Empty />}</td>
              <td className={td}>{c.phone ?? <Empty />}</td>
              <td className={td}>{c.lifecycle ?? <Empty />}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

interface TargetTableProps {
  contacts: TargetContact[];
  onEdit: (contact: TargetContact) => void;
  disabled: boolean;
}

export function TargetTable({ contacts, onEdit, disabled }: TargetTableProps) {
  return (
    <div className="relative overflow-x-auto">
      <table className="w-full text-sm">
        <caption className="sr-only">Contacts in Beta CRM, the target system</caption>
        <thead className="border-b border-border">
          <tr>
            <th scope="col" className={th}>Id</th>
            <th scope="col" className={th}>FullName</th>
            <th scope="col" className={th}>Account</th>
            <th scope="col" className={th}>Stage</th>
            <th scope="col" className={th}>Linked to</th>
            <th scope="col" className={th}>ModifiedAt</th>
            <th scope="col" className={th}>
              <span className="sr-only">Actions</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-border">
          {contacts.map((c) => (
            <tr key={c.id}>
              <td className={`${td} font-mono text-xs`}>{c.id}</td>
              <td className={td}>{c.fullName}</td>
              <td className={td}>{c.account ?? <Empty />}</td>
              <td className={td}>{c.stage}</td>
              <td className={`${td} font-mono text-xs`}>{c.externalRef ?? <span className="text-muted">not linked</span>}</td>
              <td className={`${td} text-muted`}>
                <time dateTime={c.modifiedAt}>{formatDateTime(c.modifiedAt)}</time>
              </td>
              <td className={`${td} text-right`}>
                <button type="button" className={secondaryButton} onClick={() => onEdit(c)} disabled={disabled}>
                  Edit<span className="sr-only"> {c.fullName}</span>
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
