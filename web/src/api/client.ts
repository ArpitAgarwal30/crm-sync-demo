import type { AuditEntry, ContactEdit, Plan, RollbackResult, SourceContact, SyncRun, TargetContact } from "./types";

/** An API failure, carrying the server's problem-details message and any field-level validation errors. */
export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: Record<string, string[]>;

  constructor(message: string, status: number, fieldErrors: Record<string, string[]> = {}) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

const API_UNREACHABLE =
  "Can't reach the API. Start it with `dotnet run --project src/CrmSync.Api` (it listens on port 5120), then reload this page.";

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response;
  try {
    response = await fetch(path, {
      ...init,
      headers: { Accept: "application/json", ...(init.body ? { "Content-Type": "application/json" } : {}) },
    });
  } catch {
    throw new ApiError(API_UNREACHABLE, 0);
  }

  if (!response.ok) {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
    // In development, Vite's proxy answers 502-504 with no problem details when the API isn't running.
    const gateway = response.status >= 502 && response.status <= 504 && !problem.detail && !problem.title;
    const message = gateway ? API_UNREACHABLE : (problem.detail ?? problem.title ?? `Request failed (${response.status})`);
    throw new ApiError(message, response.status, problem.errors);
  }

  return (response.status === 204 ? undefined : await response.json()) as T;
}

export const api = {
  sourceContacts: () => request<SourceContact[]>("/api/source/contacts"),
  targetContacts: () => request<TargetContact[]>("/api/target/contacts"),
  editTargetContact: (id: number, edit: ContactEdit) =>
    request<TargetContact>(`/api/target/contacts/${id}`, { method: "PUT", body: JSON.stringify(edit) }),
  createPlan: () => request<Plan>("/api/plans", { method: "POST" }),
  applyPlan: (planId: string, sourceWins: boolean) =>
    request<SyncRun>(`/api/plans/${encodeURIComponent(planId)}/apply?sourceWins=${sourceWins}`, { method: "POST" }),
  runs: () => request<SyncRun[]>("/api/runs"),
  rollback: (runId: string) => request<RollbackResult>(`/api/runs/${encodeURIComponent(runId)}/rollback`, { method: "POST" }),
  audit: (take = 50) => request<AuditEntry[]>(`/api/audit?take=${take}`),
  reset: () => request<undefined>("/api/demo/reset", { method: "POST" }),
};
