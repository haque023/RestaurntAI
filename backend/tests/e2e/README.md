# RestaurantAI end-to-end tests

Playwright tests for the MVP 1 journeys, run against an **isolated stack**: its own database, its own
RabbitMQ vhost, its own Redis DB index and its own ports. A run never reads or writes the development
database, the development audit log or the development services.

| Project | What it covers | Files |
|---|---|---|
| `api` (41 tests) | The MVP 1 API journey A1–E5 (ported from the historical `e2e_mvp1.py`), tenant isolation F1–F2, kitchen-staff authorization G1 | `api/mvp1.api.spec.ts` |
| `browser` (8 tests) | Sign-up → admin approval → restaurant setup; QR ordering → kitchen → served; role-based access; AI waiter chat | `browser/*.spec.ts` |

## Prerequisites

1. Local dependencies running: `docker compose up -d` in `backend/` (PostgreSQL, Redis, RabbitMQ).
2. A .NET 10 SDK on `PATH` and the backend built: `dotnet build backend/RestaurantAI.sln`.
   With Homebrew's keg-only formula: `export DOTNET_ROOT=/opt/homebrew/opt/dotnet@10/libexec PATH="/opt/homebrew/opt/dotnet@10/bin:$PATH"`.
3. Frontend dependencies installed: `npm install` in `frontend/`.
4. In this folder: `npm install` and, once, `npx playwright install chromium`.

## Commands (run from `backend/tests/e2e`)

```bash
npm run stack:up        # create the isolated environment (idempotent) and start the 11 services on 6080/6100/62xx/6300
npm run test:api        # API journey only (no browser, no frontends)
npm run test:browser    # browser journeys (starts the three frontends on 4100/4101/4102 against the E2E gateway)
npm test                # both projects
npm run test:live       # both projects, including the real-Claude tests (see below)
npm run report          # open the HTML report of the last run
npm run test:support    # support project: cleanup tests, historical-clone test, credential redaction canary, post-run artifact scan (no stack needed)
npm run typecheck       # TypeScript check of this package
npm run stack:down      # stop only the processes stack:up started
# Not for routine use: npm run cleanup:historical is a one-time historical procedure (see "Historical maintenance cleanup").
```

Execution order: `stack:up` → tests → `stack:down`. The stack can stay up between runs.

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `RUN_LIVE_CLAUDE_TESTS` | unset | `true` runs the real-Claude tests (API D1, D2, D4 and the browser AI waiter chat). Otherwise they are **skipped** with the reason shown. |
| `ANTHROPIC_API_KEY` | unset | Passed to the E2E `Ai.Api` by `stack:up` if set. Otherwise `Ai.Api` uses its own .NET user-secrets (`AI:Claude:ApiKey`). Never put a key in this folder. |
| `E2E_KEEP_DATA` | unset | `true` keeps the run's data for debugging instead of deleting it. |
| `E2E_DB_NAME` | `restaurant_ai_e2e` | Must end in `_e2e`; anything else is refused. |
| `E2E_PG_CONTAINER`, `E2E_RABBIT_CONTAINER` | the dev compose containers | Where the isolated database and vhost are created. |
| `DOTNET` | `dotnet` | Path to a .NET 10 `dotnet` for `stack:up`. |

The first `stack:up` writes `.run/stack.env` (git-ignored, mode 600) with the gateway URL and a **randomly
generated administrator password** for the E2E database. If the file already exists, it is left unchanged.
No credential is stored in this folder's sources.

### Rotating the E2E administrator password

`stack:up` passes the password to Identity's bootstrap, which creates the administrator **only when no
platform administrator exists**. It never changes the password of an existing account. Therefore:

- **Changing `.run/stack.env` alone does not rotate anything.** The account keeps its old password, and
  the suite then fails to sign in.
- Rotation means resetting (or recreating) the administrator account **in the isolated E2E database**, and
  writing the matching new password to `.run/stack.env`.

Procedure:

1. **Verify the target before any change.** Confirm the database is `restaurant_ai_e2e` (the `E2E_DB_NAME` in
   `.run/stack.env`) and that `e2e_meta.environment` holds the marker `restaurant-ai-e2e-isolated`. Confirm that
   exactly one platform administrator exists and that it is the configured `E2E_ADMIN_EMAIL`. Stop if any
   check fails. Never run this against `restaurant_ai` or any other database.
2. **Generate a new random password** and write it to `.run/stack.env` with mode 600, in place of the old
   value. Never put it in source files, command-line arguments, shell history, logs or reports.
3. **Reset the account in the E2E database.** Replace only that account's password hash, in the format
   Identity uses (`PasswordHasher`: PBKDF2-SHA256, `pbkdf2-sha256$<iterations>$<salt>$<hash>`, base64). Use
   a single transaction that changes exactly that one row of the marked database and rolls back otherwise.
   Recreating the account instead (delete it so bootstrap creates it again on the next `stack:up`) is more
   invasive: it gives the administrator a new id and emits new registration events.
4. **Verify through the normal sign-in:** with the stack up, the new password signs in with
   `scope: "Admin"` and the token carries the platform-administrator role. **The old password must be
   rejected** (`401 auth.invalid_credentials`). Print status codes only, never a credential or token.
5. **Re-scan the artifacts** (`npm run test:support`, after a test run) and delete any temporary copy of the
   old value.

Access tokens already issued are signed tokens with no server-side session. Changing the password does not
revoke them, and they stay valid until they expire.

## Test data and cleanup

Every run gets an id (`e2e` + 8 hex characters) that appears in every restaurant name and email. Every
record a test creates is written to `.run/registry-<run>.jsonl` first. The registry is a claim, not
proof; teardown (`support/global-teardown.ts`, `support/db.ts`) proceeds as follows:

1. **Isolation guard, before any other statement:** the database name must match `*_e2e` (and not
   `restaurant_ai`), and `e2e_meta.environment` must hold the marker row. Otherwise it aborts with no change.
2. **Registry validation:** each line must be JSON with a UUID id and a name or email carrying the run id
   (`(guest)` for anonymous guests). An invalid registry is reported as unresolved, and nothing is deleted for that run.
3. **Ownership verification, per id:**
   - **Tenant:** its restaurant row names the run, or, once that row is gone, its `RestaurantRegistered` event does.
   - **User:** its account email carries the run, or, once the account is gone, its `UserRegistered` event's email does.
   - A record that exists but does not carry the run id aborts the cleanup.
   - Ids without evidence (and guests) are reported as unverified, and nothing is deleted on their behalf.
4. **Delete, in one transaction under `ON_ERROR_STOP`, children before parents:**
   - Rows match on the verified tenant ids (`tenant_id`, restaurant `id`).
   - Or on the verified user ids (`user_id`, `customer_id`, `actor_id`, account `id`).
   - For event tables (`platform.audit_events` and every `*.outbox_messages`), also on `payload.userId` / `payload.tenantId`.
   - Event rows that name a different run id are kept and reported (`mismatchedEventsKept`).
   - All values are psql variables (`:'name'`), never concatenated SQL.
5. **Independent verification:**
   - (a) The ownership rules must match nothing any more.
   - (b) A database-wide scan of every text, varchar, char, array and JSON column, in every table except
     `e2e_meta` and the migration histories, looks for the run id. A hit that also references a registered
     id is **confirmed** (cleanup status `incomplete`, and teardown fails). Any other hit is **ambiguous**
     (status `needs-review`, kept for manual review). The scan never deletes.
   - (c) The administrator's account and registration events must be unchanged.
6. **Stale-run sweep:** earlier runs whose registry is not marked `.done` go through the same path. A
   registry is marked `.done` only when its cleanup is `complete`.
7. **Report:** the summary (`.run/run-<run>.json`) lists the audit events created, rows deleted by table, the
   residual scan (including the columns inspected), unverified ids and whether the development database
   fingerprint is unchanged.

Not removed: discovery-assistant chat history in the E2E Redis DB (it expires on its own), and messages in the E2E RabbitMQ vhost.

### Support tests

`npm run test:support` runs the `support` project of `playwright.support.config.ts`:
- the cleanup tests (`cleanup.spec.ts`);
- the historical-clone test (`history-clone.spec.ts`);
- the credential redaction canary (`redaction.spec.ts`);
- the post-run artifact scan (`artifact-scan.spec.ts`).

It excludes the `maintenance` project. It needs no stack, only the Postgres container and the E2E
database's schema. The artifact scan is meaningful only after a test run.
- It creates and drops its own databases: `restaurant_ai_cleanuptest_e2e`, `restaurant_ai_nomarker_e2e`,
  and `restaurant_ai_historyclone_e2e`, a full copy of the E2E database used to show what the cleanup would
  do to historical runs.
- It only reads the E2E database (`pg_dump`).

`npm run typecheck` type-checks this package with the frontend's TypeScript.

### Historical maintenance cleanup (one-time procedure, not a general command)

`npm run cleanup:historical` runs the `maintenance` project (`support-tests/maintenance/historical-cleanup.spec.ts`).
It was written for **one** operation, which has already been carried out: removing 27 outbox rows that early runs had
left in the E2E database, before teardown verified outbox ownership. It is **not** a repeatable cleanup command.

- **Separate from normal use.** `npm test`, `test:api`, `test:browser`, `test:live` and global teardown never run
  it. `npm run test:support` selects only the `support` project. Running `playwright.support.config.ts`
  *without* `--project` would also run the maintenance project's step 1, which only touches a disposable copy.
- **Step 1** (always): applies the teardown cleanup path (`cleanupRegistries`) to every completed `.done`
  registry, against a disposable full copy (`restaurant_ai_historyclone_e2e`). It records the result in
  `test-results/historical-clone-evidence.json`.
- **Step 2** (real E2E database): skipped unless **`E2E_MAINT_APPLY=restaurant_ai_e2e`** is set exactly. It also
  requires the step-1 evidence, an isolated E2E database, and the same registries, candidate rows,
  administrator records and table counts that step 1 validated. Otherwise it deletes nothing.
- **Its expected values are historical.** The spec asserts **7 registries** and **27 candidate outbox rows**.
  These describe the original operation, not the current state, and must not be treated as current expected
  values. More registries exist now, so step 1 would fail on those assertions as written.
- **Before any reuse**, inspect the current E2E database and `.run/` registries. Review every count and
  fixture assumption in the spec, and get explicit approval. Then validate on the disposable copy first.

## Live Claude tests

They are opt-in because they cost money and depend on an external provider. Each kind has its own,
deliberately different policy:

- **API** (D1, D2, D4): each call has a 180 s request timeout and at most one retry on a 5xx
  response (`aiTurn` in `api/mvp1.api.spec.ts`). Token usage is reported only as recorded by `Ai.Api`
  from Claude's own response (the `provider-usage` annotation); nothing is estimated.
- **Browser** (AI waiter chat): one attempt, no retry, and up to 120 s for the reply bubble to
  appear. A retry would mean sending a second chat message from the page.

Without live Claude, the AI tests that need no model still run, and none of them is designed to reach the
provider:

- **D5** checks the switch on the restaurant service, then waits for the AI service's 60 s context
  cache by polling the non-billable `GET /api/ai/usage/current`. That endpoint shares the waiter's
  per-tenant cache entry. D5 then sends exactly one waiter message, which the application refuses
  (`ai.disabled`) before any model call. With live tests on, the same holds: no "hello" turns are sent
  while the cache still says "enabled". D5 is not gated by `RUN_LIVE_CLAUDE_TESTS` (see
  [Known limitations](#known-limitations)).
- **D6–D9**: plan, usage and the agent registry.
- **E2**: calls the MCP tool `search_restaurants` directly with a customer token.

## Credentials and test artifacts

- **Passwords:** test passwords come from a random secret created per run (`E2E_PASSWORD_SECRET`). They
  can't be derived from the run id that appears in reports. The secret is kept in
  `.run/secrets/run-<run>.secret` (mode 600, git-ignored) only so that the artifact scan can search for it.
  The stack administrator's random password lives in `.run/stack.env`.
- **Artifact policy** (`support/artifacts.ts`, shared by every config):
  - **traces off**: Playwright records action parameters (typed values, evaluate arguments) in traces and cannot redact them;
  - **videos off**;
  - **screenshots only on failure**: password fields render masked.
- **Typing credentials:** always through `fillSecret` (`browser/helpers.ts`). Plain `locator.fill(password)`
  puts the value into the HTML report: this was demonstrated, not assumed.
- **Verification:**
  - `support-tests/redaction.spec.ts` runs two deliberately failing browser tests under this policy and
    searches every artifact for canary values, including the archive embedded in the HTML report and
    any zip. The `fillSecret` canary must appear nowhere. The plain-`fill` control must be found, which
    proves the search works.
  - `support-tests/artifact-scan.spec.ts` searches the latest reports, test output, run summaries and
    stack logs for the administrator password and every recorded run secret. It prints counts only.
    Run `npm run test:support` after a test run to include it.

## Test synchronization: Restaurant → Menu navigation (cause unconfirmed)

The onboarding browser test adds a branch on the Restaurant page, clicks **Menu**, and then fills the
"New menu item" form. Without a wait, the menu item could be lost. **The root cause has not been
established.**

What is known:

- Right after the click on **Menu**, `getByLabel("Name")` can still match the Restaurant page's
  **Add branch** "Name" field, before client-side navigation has replaced the page.
- The code does not support the earlier explanation, that the form mounts before the branch resolves
  and is then recreated. `MenuPage.tsx` renders the form only once a branch id is available. This README
  makes no claim that the application form remounts or loses input.

The test therefore waits for a heading that exists only on the Menu page ("No menu items yet") before
interacting with the form. This is a **test synchronization workaround**. It is valid whatever the cause
turns out to be. It is not evidence about the form's behavior while the page loads. The application was not changed.

## Known limitations

These are open; none of them is solved by the current suite.

- **Failure messages can include response bodies.** `Api.ok()` (`support/api.ts`) puts the response body
  (first 400 characters) into an assertion failure message. A status mismatch on a sign-in response could
  write a short-lived E2E access token into the test report. Request bodies, and therefore passwords, are
  not included.
- **D5 does not prove the provider can never be called.** Without live tests, D5 verifies that the
  disabled switch is enforced and that the single probe is refused. It runs regardless of
  `RUN_LIVE_CLAUDE_TESTS`. If application enforcement regressed so that the check ran after the model call,
  that probe could reach the provider (`Ai.Api` reads its key from its own user-secrets).
- **G1 re-checks only menu availability.** After the refused calls, G1 confirms that the menu item's
  availability is unchanged. It does not independently re-check the restaurant profile or the table list.
- **Live Claude paths have not been executed:** API D1, D2, D4, the browser AI waiter chat, and D5 with live tests on.
- **No linter** is configured for this package. Only `npm run typecheck` runs.
- **Redis and RabbitMQ test data are not cleaned:** discovery chat history in the E2E Redis DB (it expires on its
  own), and messages in the E2E RabbitMQ vhost. Cleanup covers the E2E PostgreSQL database only.
- **Per-run secret files stay on disk:** `.run/secrets/run-<run>.secret` (git-ignored, mode 600) is kept so
  that the artifact scan can search for it. The accounts it belonged to are deleted by teardown.

## Provenance

Each test carries a `provenance` annotation:
- **recovered**: one-to-one port of a check from the historical suite. That suite was a Python script; its 38 checks were recovered verbatim from the development session transcript.
- **recovered-adapted**: the historical check, with the change stated in the annotation. Examples are live gating and bounded polls instead of fixed sleeps.
- **new**: added with this suite. This covers F1–F2, G1 and every browser test; the historical suite had no browser automation.
