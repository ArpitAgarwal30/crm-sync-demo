import type { ReactNode } from "react";

interface PanelProps {
  id: string;
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
  children: ReactNode;
}

export function Panel({ id, title, description, actions, children }: PanelProps) {
  const headingId = `${id}-heading`;
  return (
    <section aria-labelledby={headingId} className="flex min-w-0 flex-col rounded-xl border border-border bg-surface">
      <header className="flex flex-wrap items-start justify-between gap-3 border-b border-border px-5 py-4">
        <div className="min-w-0">
          <h2 id={headingId} className="font-semibold">
            {title}
          </h2>
          {description && <p className="mt-1 text-sm text-muted">{description}</p>}
        </div>
        {actions}
      </header>
      <div className="min-w-0 flex-1 p-5">{children}</div>
    </section>
  );
}

export const primaryButton =
  "inline-flex items-center justify-center gap-2 rounded-md bg-accent px-4 py-2 text-sm font-medium text-accent-contrast transition-opacity hover:opacity-90 disabled:cursor-not-allowed disabled:opacity-50";

export const secondaryButton =
  "inline-flex items-center justify-center gap-2 rounded-md border border-border px-3 py-1.5 text-sm font-medium transition-colors hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-50";
