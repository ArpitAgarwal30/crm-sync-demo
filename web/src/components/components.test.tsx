import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { Plan, PlannedChange } from "../api/types";
import { ChangeList } from "./ChangeList";
import { PlanPanel } from "./PlanPanel";

afterEach(cleanup);

const update: PlannedChange = {
  action: "Update",
  sourceId: "A-1004",
  displayName: "Tomas Silva",
  targetId: 5003,
  changes: [
    { field: "PhoneNumber", from: null, to: "5550101969" },
    { field: "Stage", from: "Prospect", to: "Client" },
  ],
  reason: "Matched by email; will be linked to the source record",
};

const conflict: PlannedChange = {
  action: "Conflict",
  sourceId: "A-1005",
  displayName: "Hannah Weber",
  targetId: 5004,
  changes: [{ field: "Account", from: "Alpine Retail (EU)", to: "Alpine Retail Group" }],
  reason: "Edited in Beta CRM since the last sync",
};

const plan: Plan = {
  id: "plan-1",
  createdAt: "2026-10-12T10:00:00Z",
  appliedRunId: null,
  summary: { create: 0, update: 1, unchanged: 0, conflict: 1, invalid: 0, unmanaged: 1 },
  changes: [update, conflict],
  unmanagedTargetIds: [5006],
};

describe("ChangeList", () => {
  it("shows each field's current and new value, with empty values spelled out", () => {
    render(<ChangeList changes={[update]} />);

    const phone = screen.getByRole("row", { name: /PhoneNumber/ });
    expect(within(phone).getByText("(empty)")).toBeTruthy();
    expect(within(phone).getByText("5550101969")).toBeTruthy();
    expect(screen.getByText("Matched by email; will be linked to the source record")).toBeTruthy();
    expect(screen.getByText("A-1004 → #5003")).toBeTruthy();
  });
});

describe("PlanPanel", () => {
  const props = { lastRun: null, betaChangedSincePlan: false, busy: false, onCreatePlan: vi.fn() };

  it("applies without overwriting conflicts by default", async () => {
    const onApply = vi.fn();
    render(<PlanPanel {...props} plan={plan} onApply={onApply} />);

    await userEvent.click(screen.getByRole("button", { name: "Apply this plan" }));

    expect(onApply).toHaveBeenCalledWith(false);
  });

  it("passes source-wins only when the visitor opts in", async () => {
    const onApply = vi.fn();
    render(<PlanPanel {...props} plan={plan} onApply={onApply} />);

    await userEvent.click(screen.getByRole("checkbox", { name: /Let the source win conflicts/ }));
    await userEvent.click(screen.getByRole("button", { name: "Apply this plan" }));

    expect(onApply).toHaveBeenCalledWith(true);
  });

  it("explains that unmanaged contacts are never deleted", () => {
    render(<PlanPanel {...props} plan={plan} onApply={vi.fn()} />);

    expect(screen.getByText(/#5006 have no source match/)).toBeTruthy();
  });

  it("warns when Beta changed after the dry run", () => {
    render(<PlanPanel {...props} betaChangedSincePlan plan={plan} onApply={vi.fn()} />);

    expect(screen.getByText(/Beta CRM changed after this dry run/)).toBeTruthy();
  });
});
