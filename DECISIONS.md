# DECISIONS

> Written in English for convenience — native language of the codebase, no RTL/LTR mixing with code.

## 1. Architectural Decisions

### Backend
- `LeaveRequestsController` split into a thin Controller (HTTP ↔ service) and `LeaveRequestService` (quota checks, approve transaction, validation). No Repository layer — `DbContext` already is one, and more layering would just add boilerplate.
- Responses use `LeaveRequestDto`/`EmployeeDto` instead of EF entities — decouples the API from the DB model and avoids the `Employee`↔`LeaveRequest` circular-navigation issue.
- Errors use `ServiceResult<T>` with `ErrorType` (`NotFound`/`Conflict`/`Invalid`), mapped by the controller to 404/409/400 — no exception hierarchy needed.

### Frontend
- **Form**: Reactive Form, `Validators.required` on employee/type/dates + a cross-field `startDate <= endDate` check (also guarantees a non-negative day count, since days are server-computed). Per-field errors, submit feedback.
- **Approve button**: per-row `approvingIds` state disables only that row and shows "Approving...". Success patches just that row and shows an auto-dismissing toast (no `alert`); failure (e.g. 409 already-approved/quota) shows the server message under that row.
- **Cleanup**: HTTP calls moved to `LeaveRequestsService` (typed `Observable`s), no more `any`, subscriptions use `takeUntilDestroyed`.

## 2. Leave Balance Bug
- `Create` computed `used` (approved days) but never checked it — condition was only `days > AnnualQuota`, ignoring prior usage. Fixed to `used + days > AnnualQuota`.
- Test: `Create_ExceedingRemainingQuota_FailsEvenThoughSingleRequestFitsAnnualQuota` (quota 10, 8 approved, +3 days rejected).

## 3. Approve Request & Concurrency
- `POST /api/leave-requests/{id}/approve`: not found → `404`; already `Approved`/`Rejected` → `409`; would exceed quota → `409`, stays `Pending`.
- Risk: two `Pending` requests that together exceed quota could both read a stale `used` and both pass if approved concurrently.
- Fix: status check + `used` calc + update in one transaction; PostgreSQL locks the Employee row (`SELECT ... FOR UPDATE`) to serialize this. SQLite (used here) has no real row locking — its whole-file write lock hides the issue rather than solving it.
- More time: add a `RowVersion`/`xmin` token as a second line of defense.

## 4. What I Gave Up Due to Time
- All required tasks were implemented and verified end-to-end in the running UI before time ran out for extra polish.
- Didn't verify the concurrency fix on a live PostgreSQL instance — Docker was blocked by a company policy (section 6); the Postgres path is coded/reasoned, not run end-to-end. Would otherwise try a workaround (personal Docker Hub org, Podman) or a private laptop.
- No extra manual/unit testing beyond what's required — no Angular component/service tests, no further backend edge cases.

## 5. AI Usage

### Where AI helped (with prompts)
AI helped with essentially everything, step by step inside the IDE — these are classic examples:
1. *"There's a bug in the leave balance check... Find it, fix it, add a test."* → found the unchecked `used` variable, fixed it, added a regression test in one pass.
2. *"Add `POST /api/leave-requests/{id}/approve` ... think about two approvals racing ... use transactions/locking if on Postgres."* → correct status codes, a transaction, `SELECT ... FOR UPDATE` for Postgres, documented SQLite fallback.
3. Git workflow: organized and pushed one focused commit per task with descriptive messages, done alongside the implementation.

### Where I rejected/corrected an AI suggestion
- Approve button: AI handled loading/error but missed "success feedback" from the prompt. Caught before commit and fixed.

### Security
- **SQL Injection** (found & fixed): `Search` built SQL via string concatenation (`LIKE '%" + name + "%'"`), exploitable via the `name` parameter. Fixed with `EF.Functions.Like`, which parameterizes automatically.

## 6. How to Run
- Docker wasn't installed initially, so I used SQLite (`Database__Provider=Sqlite`) for the backend and plain `npm start` for the frontend.
- Later installed Docker Desktop and attempted migrating to PostgreSQL via `docker compose up`, but it enforces org-restricted sign-in (`verint...`) and signed me out — *"account is not a member of the [verint...] organization(s)."* The laptop is fine for personal use, but this policy ties Docker Desktop to an org account a personal Docker Hub account can't satisfy.
- Stayed on SQLite for the submission; the Postgres locking path (section 3) is implemented but verified only by code review and SQLite-equivalent tests, not a live container.
