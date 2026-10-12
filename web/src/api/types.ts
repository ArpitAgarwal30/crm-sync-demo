// Mirrors the JSON returned by CrmSync.Api.

/** Alpha CRM (source) keeps its own snake_case field names, exactly as the API returns them. */
export interface SourceContact {
  id: string;
  first_name: string | null;
  last_name: string | null;
  email_address: string | null;
  phone: string | null;
  company: string | null;
  lifecycle: string | null;
}

export interface TargetContact {
  id: number;
  externalRef: string | null;
  fullName: string;
  email: string | null;
  phoneNumber: string | null;
  account: string | null;
  stage: string;
  modifiedAt: string;
}

export type ChangeAction = "Create" | "Update" | "Unchanged" | "Conflict" | "Invalid";

export interface FieldChange {
  field: string;
  from: string | null;
  to: string | null;
}

export interface PlannedChange {
  action: ChangeAction;
  sourceId: string;
  displayName: string;
  targetId: number | null;
  changes: FieldChange[];
  reason: string | null;
}

export interface Plan {
  id: string;
  createdAt: string;
  appliedRunId: string | null;
  summary: { create: number; update: number; unchanged: number; conflict: number; invalid: number; unmanaged: number };
  changes: PlannedChange[];
  unmanagedTargetIds: number[];
}

export interface SyncRun {
  runId: string;
  appliedAt: string;
  applied: { action: ChangeAction; sourceId: string; targetId: number; changes: FieldChange[] }[];
  skipped: { sourceId: string; action: ChangeAction; reason: string }[];
  rolledBackAt: string | null;
}

export interface RollbackResult {
  run: SyncRun;
  restored: number[];
  refused: { targetId: number; reason: string }[];
}

export interface AuditEntry {
  id: number;
  at: string;
  runId: string;
  event: string;
  sourceId: string;
  targetId: number | null;
  detail: string;
}

export interface ContactEdit {
  fullName: string;
  email: string | null;
  phoneNumber: string | null;
  account: string | null;
  stage: string;
}
