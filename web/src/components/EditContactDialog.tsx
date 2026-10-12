import { useEffect, useRef, useState, type FormEvent } from "react";
import { ApiError } from "../api/client";
import type { ContactEdit, TargetContact } from "../api/types";
import { primaryButton, secondaryButton } from "./Panel";

interface EditContactDialogProps {
  contact: TargetContact | null;
  onClose: () => void;
  onSave: (edit: ContactEdit) => Promise<void>;
}

/** Simulates a Beta CRM user editing a contact, so the next dry run can show a conflict. */
export function EditContactDialog({ contact, onClose, onSave }: EditContactDialogProps) {
  const dialog = useRef<HTMLDialogElement>(null);

  // The native dialog provides focus trapping, Escape to close and returning focus on close.
  useEffect(() => {
    const element = dialog.current;
    if (contact && !element?.open) element?.showModal();
    if (!contact && element?.open) element.close();
  }, [contact]);

  return (
    <dialog
      ref={dialog}
      onClose={onClose}
      aria-labelledby="edit-heading"
      className="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-xl border border-border bg-surface p-0 text-text"
    >
      {contact && <EditForm key={contact.id} contact={contact} onCancel={onClose} onSave={onSave} />}
    </dialog>
  );
}

function EditForm({ contact, onCancel, onSave }: { contact: TargetContact; onCancel: () => void; onSave: (edit: ContactEdit) => Promise<void> }) {
  const [edit, setEdit] = useState<ContactEdit>({
    fullName: contact.fullName,
    email: contact.email,
    phoneNumber: contact.phoneNumber,
    account: contact.account,
    stage: contact.stage,
  });
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const [message, setMessage] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function submit(event: FormEvent) {
    event.preventDefault();
    setSaving(true);
    try {
      await onSave(edit);
    } catch (error) {
      const fieldErrors = error instanceof ApiError ? error.fieldErrors : {};
      setErrors(fieldErrors);
      setMessage(Object.keys(fieldErrors).length > 0 ? "Fix the highlighted fields." : error instanceof Error ? error.message : "Saving failed.");
      setSaving(false);
    }
  }

  const field = (key: keyof ContactEdit, label: string, apiName: string) => {
    const error = errors[apiName]?.[0];
    return (
      <div>
        <label htmlFor={`edit-${key}`} className="block text-sm font-medium">
          {label}
        </label>
        <input
          id={`edit-${key}`}
          value={edit[key] ?? ""}
          onChange={(e) => {
            setEdit({ ...edit, [key]: e.target.value === "" && key !== "fullName" ? null : e.target.value });
            // The field changed, so its old server error no longer applies.
            setErrors((current) => Object.fromEntries(Object.entries(current).filter(([name]) => name !== apiName)));
          }}
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `edit-${key}-error` : undefined}
          className="mt-1 w-full rounded-md border border-border bg-bg px-3 py-2 text-sm"
        />
        {error && (
          <p id={`edit-${key}-error`} className="mt-1 text-sm text-invalid">
            {error}
          </p>
        )}
      </div>
    );
  };

  return (
    <form onSubmit={submit} className="space-y-4 p-6">
      <div>
        <h2 id="edit-heading" className="text-lg font-semibold">
          Edit contact #{contact.id} in Beta CRM
        </h2>
        <p className="mt-1 text-sm text-muted">
          This simulates a Beta CRM user changing the record. The sync didn't make this change, so the next dry run treats it
          as someone else's work.
        </p>
      </div>
      {field("fullName", "Full name", "FullName")}
      {field("email", "Email", "Email")}
      {field("phoneNumber", "Phone number", "PhoneNumber")}
      {field("account", "Account", "Account")}
      <div>
        <label htmlFor="edit-stage" className="block text-sm font-medium">
          Stage
        </label>
        <select
          id="edit-stage"
          value={edit.stage}
          onChange={(e) => setEdit({ ...edit, stage: e.target.value })}
          className="mt-1 w-full rounded-md border border-border bg-bg px-3 py-2 text-sm"
        >
          <option>Prospect</option>
          <option>Client</option>
        </select>
      </div>
      {message && (
        <p role="alert" className="text-sm text-invalid">
          {message}
        </p>
      )}
      <div className="flex justify-end gap-2 pt-2">
        <button type="button" className={secondaryButton} onClick={onCancel}>
          Cancel
        </button>
        <button type="submit" className={primaryButton} disabled={saving}>
          {saving ? "Saving…" : "Save in Beta CRM"}
        </button>
      </div>
    </form>
  );
}
