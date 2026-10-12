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
- **Pure core.** `CrmSync.Core` has no file, network or console access; the CLI owns all I/O. That keeps the logic fully unit-testable with a controllable clock.

## Project layout

```
src/CrmSync.Core/    Sync engine: mapping, matching, planning, applying, rollback, audit
src/CrmSync.Cli/     Command-line app and file storage for the demo
tests/               xUnit tests for the engine
demo/                Mock source data, target seed data and sync-state seed
```

## Run it

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

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
- [ ] **D2:** ASP.NET Core API
- [ ] **D3:** React/TypeScript UI
- [ ] **D4:** deployment and write-up

Known limitations of D1: file storage is not transactional across files (an interrupted apply could save the target but not the run record); the API step will move storage to a database with transactions.
