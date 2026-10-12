import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, api } from "./client";

function respond(status: number, body?: unknown) {
  vi.stubGlobal(
    "fetch",
    vi.fn(async () => new Response(body === undefined ? null : JSON.stringify(body), { status })),
  );
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("api client", () => {
  it("surfaces the server's problem-details message", async () => {
    respond(409, { title: "Conflict", detail: "Plan 'p1' was already applied as run 'r1'. Create a new plan." });

    await expect(api.applyPlan("p1", false)).rejects.toMatchObject({ status: 409, message: expect.stringContaining("already applied") });
  });

  it("keeps field-level validation errors", async () => {
    respond(400, { title: "One or more validation errors occurred.", errors: { Stage: ["Stage must be one of: Prospect, Client."] } });

    const error = await api.editTargetContact(5002, { fullName: "X", email: null, phoneNumber: null, account: null, stage: "Partner" }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).fieldErrors.Stage).toEqual(["Stage must be one of: Prospect, Client."]);
  });

  it("treats 204 No Content as success with no body", async () => {
    respond(204);

    await expect(api.reset()).resolves.toBeUndefined();
  });

  it("explains when the API can't be reached", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => Promise.reject(new TypeError("Failed to fetch"))));

    await expect(api.runs()).rejects.toMatchObject({ status: 0, message: expect.stringContaining("Can't reach the API") });
  });

  it("explains a dev-proxy 502 as the API not running", async () => {
    respond(502);

    await expect(api.targetContacts()).rejects.toMatchObject({ status: 502, message: expect.stringContaining("Can't reach the API") });
  });

  it("still shows a real server message on a 503 that has problem details", async () => {
    respond(503, { title: "Service Unavailable", detail: "Database is being reset." });

    await expect(api.targetContacts()).rejects.toMatchObject({ message: "Database is being reset." });
  });

  it("encodes ids in URLs", async () => {
    respond(200, []);

    await api.rollback("run/../x");

    expect(vi.mocked(fetch).mock.calls[0]?.[0]).toBe("/api/runs/run%2F..%2Fx/rollback");
  });
});
