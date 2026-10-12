import type { ChangeAction } from "../api/types";

const color: Record<ChangeAction, string> = {
  Create: "text-create",
  Update: "text-update",
  Unchanged: "text-muted",
  Conflict: "text-conflict",
  Invalid: "text-invalid",
};

export function ActionBadge({ action }: { action: ChangeAction }) {
  return (
    <span className={`inline-flex shrink-0 rounded-full border border-current px-2 py-0.5 font-mono text-xs uppercase ${color[action]}`}>
      {action}
    </span>
  );
}
