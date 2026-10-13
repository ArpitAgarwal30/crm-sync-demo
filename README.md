# CRM Sync Demo

An independent demo of syncing contacts between two CRMs **safely**: preview every change before it happens, never overwrite someone else's edits silently, never delete, and undo any run.

> Both CRMs are mocks with fictional data (reserved `.example` domains). This project contains no code or data from any employer or customer.

## The problem it models

Two systems hold the same customers in different shapes:

| | Alpha CRM (source) | Beta CRM (target) |
|---|---|---|
| Field style | `snake_case` | `PascalCase` |
| Name | `first_name` + `last_name` | one `FullName` |
| Stage | `lead` / `customer` | `Prospect` / `Client` |
| Ids | `A-1001` | `5001`, plus `ExternalRef` linking back to the source |

People also edit Beta directly, so a naive "copy everything over" sync would destroy their work.

## What the sync does

1. **Map and validate** each source record into a canonical contact (trimmed, lower-cased email, digits-only phone). Records that can't be mapped safely (bad email, unknown lifecycle) are reported as *invalid*, never guessed at.
2. **Match** to the target: by `ExternalRef` first, then by email for unlinked contacts (which then get linked).
3. **Plan (dry run)**: create, update, unchanged, conflict or invalid, with a field-by-field diff. Nothing changes.
4. **Apply** the plan, skipping conflicts unless told the source wins.
5. **Roll back** any run from the before-images it recorded.

Every applied run is written to an append-only audit log.

## Design decisions

- **Diff in the target's shape.** Comparing canonical models would be lossy: "Mary Ann Castillo" can't be reliably split back into first and last name, so it would look changed on every run. Comparing Beta's own fields avoids that churn.
- **Three-way conflict detection.** The sync remembers the `ModifiedAt` of every record as it last wrote it. If that value has moved, someone edited the record in Beta, and a differing source value is a *conflict*, not an update.
- **Optimistic concurrency.** A plan records each target's version. If the record changes between planning and applying, that change is skipped as stale, so replaying an old plan can't duplicate or clobber anything.
- **No deletes.** Target contacts with no source match are reported as unmanaged and left alone. Rollback only removes contacts the sync itself created.
- **Safe rollback.** Rollback refuses to restore any contact edited after the run, and reports it instead.
- **Pure core.** `CrmSync.Core` has no file, network or console access; the CLI and API own all I/O. That keeps the logic fully unit-testable with a controllable clock.
- **All-or-nothing writes (API).** Applying or rolling back runs inside one SQLite transaction: the target CRM, sync state, run record and audit entries are saved together or not at all.
- **Plans are stored.** `POST /api/plans` saves the dry run; `POST /api/plans/{id}/apply` applies exactly that plan, once. Anything edited in between is caught by the stale check instead of being overwritten.
- **Lossless timestamps.** Conflict and rollback checks compare timestamps exactly, so the database stores them as UTC ticks (EF Core's default SQLite conversion would lose precision and break those checks).

## Project layout

```
src/CrmSync.Core/        Sync engine: mapping, matching, planning, applying, rollback, audit
src/CrmSync.Api/         ASP.NET Core API, EF Core + SQLite storage
web/                     React + TypeScript UI (Vite, Tailwind CSS)
src/CrmSync.Cli/         Command-line app with file storage
tests/CrmSync.Core.Tests xUnit unit tests for the engine
tests/CrmSync.Api.Tests  Integration tests over HTTP against a throwaway SQLite database
demo/                    Mock source data, target seed data and sync-state seed (used by API and CLI)
```

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

### API

```bash
dotnet run --project src/CrmSync.Api
```

It listens on `http://localhost:5120`, creates and seeds `src/CrmSync.Api/App_Data/crmsync.db` on first start, and serves its OpenAPI document at `/openapi/v1.json` in Development.

| Method | Path | What it does |
|---|---|---|
| `GET` | `/api/source/contacts`, `/api/target/contacts` | Read either mock CRM |
| `PUT` | `/api/target/contacts/{id}` | Edit a contact directly in Beta, as one of its users would (use it to create a conflict) |
| `POST` | `/api/plans` | Create a dry-run plan; nothing changes |
| `GET` | `/api/plans/{planId}` | Read a stored plan |
| `POST` | `/api/plans/{planId}/apply?sourceWins=false` | Apply a plan once; `409` if it was already applied |
| `GET` | `/api/runs`, `/api/runs/{runId}` | Applied runs with before/after images |
| `POST` | `/api/runs/{runId}/rollback` | Undo a run; `409` if already rolled back |
| `GET` | `/api/audit?take=100` | Audit log, newest first |
| `POST` | `/api/demo/reset` | Restore both mock CRMs and clear history |
| `GET` | `/health` | Health check |

Errors use standard problem details (`application/problem+json`).

### UI

With the API running, in another terminal (requires Node 24):

```bash
cd web
npm install
npm run dev
```

Open `http://localhost:5174`. Vite forwards `/api` to the API on port 5120, so there is no CORS setup. The page shows both CRMs side by side, the dry run with field-by-field changes, applied runs with rollback, and the audit log. Use **Edit** on a Beta contact, then create a new dry run, to see a conflict.

UI checks: `npm run typecheck`, `npm run lint`, `npm test` (Vitest + Testing Library), `npm run build`.

### CLI

```bash
dotnet test
dotnet run --project src/CrmSync.Cli -- plan
dotnet run --project src/CrmSync.Cli -- apply
dotnet run --project src/CrmSync.Cli -- runs
dotnet run --project src/CrmSync.Cli -- rollback <run id>
dotnet run --project src/CrmSync.Cli -- audit
dotnet run --project src/CrmSync.Cli -- reset
```

Run commands from the repository root. `apply --source-wins` overwrites conflicts. The demo data covers every case in one run: a create, an update, an email match that gets linked, two unchanged contacts (one with a two-word first name), a conflict, two invalid records, and one unmanaged contact.

## Status and next steps

- [x] **D1:** sync engine, tests and CLI
- [x] **D2:** ASP.NET Core API with transactional SQLite storage
- [x] **D3:** React/TypeScript UI
- [ ] Case study on [the author's portfolio](https://arpitagarwal.vercel.app)

There is no hosted copy; run it locally as described above.

Known limitations:

- The CLI keeps its simpler file storage, which is not transactional across files; use the API for all-or-nothing writes.
- The API has no authentication. It is a local demo; anything deployed publicly would need it, or at least a read-only mode.
- The database schema is created with `EnsureCreated` rather than migrations, which is fine for disposable demo data but not for a real system.

## License

[MIT](LICENSE)
