# Spec: Kernel tracking (watch a conversation without sleeping)

Eng-cleared 2026-09-04 (locks applied below). Personality: **inspectable kernel** — catalogs you can read, named failures, no catch-all. This slice is a **test-time conversation runner**: a session that drains immediate cascades on the caller’s thread and records what happened. It is not a production wait and not LocalQueues.

Fills the Tracking slot in the 12-month kernel. Does **not** amend that kernel except this iteration’s Application/Tracking scope. The 12-month Helpdesk `TrackActivity` acceptance (`PlaceOrder` → ChargePayment attempts → timeout `NotFound`) remains a named follow-up. Where this document is silent, the kernel still governs. `PlayDue` stays Scheduling’s drain; Tracking wraps one snapshot and then until-quiet.

---

## 1. Outcomes & Why

`ICascadePublisher` must not Invoke the next handler. Immediate `Publish` still does not run handlers (LocalQueues later). Tests that `Invoke` a message whose success cascades `ChargePayment` therefore never see `ChargePayment` execute — unless something test-scoped drains those immediate descendants through Execution.

Handlers must not `Task.Delay`. Scheduling already parks `[Timeout]` / delayed Publish / `ScheduleRetry` and plays them with `PlayDue(asOf)`. Tests still need a **session** they can assert: what was published, what executed (including failed attempts), what remains scheduled — without xUnit helpers living in Application, and without making Tracking the host’s wait.

**Why now:** Discovery, Bus, Mediator, Cascades, Routing, Execution, Middleware, Scheduling, and Application/Sagas are on `main`. `TrackingPlan` is the remaining Application slice. LocalQueues, Hosting, Persistence, and Helpdesk teaching types are still later.

**Success:** a `MiniVerine.Tests` fixture conversation — start message cascades a flaky charge; attempts 1–2 throw; attempt 3 succeeds and cascades a paid event — is asserted on the session as three charge **executions** and one paid **execution**, with no `Thread.Sleep`. A delayed timeout from that conversation is absent from the first session’s Executed bag, then present on the **new** session returned from `PlayScheduledMessagesAsync(asOf)`.

---

## 2. Scope

**In:**

- First-class bus API tests use (not a `tests/` helper): one-shot tracked Invoke and tracked Publish
- Until-quiet drain of **cascade returns only** through Execution on the caller’s thread
- Tracked Publish executes the root on that thread, then until-quiet (this is the one place Publish runs handlers before LocalQueues)
- Session bags: Published, Executed, Scheduled; Executed is **per handler attempt**
- `PlayScheduledMessagesAsync(asOf)`: one `PlayDue(asOf)`, then until-quiet for that snapshot’s immediate cascades, return a **new** session whose bags are that play only
- Fail-fast: first descendant `HandlerFault` stops the drain and bubbles; `HandlerFault.Session` holds the session so far (null when untracked)
- README: Tracking moves from Plan-only to done-as-test-session; `TrackingPlan` is removed when types exist
- Prove-with in `MiniVerine.Tests` with fixture messages (not Helpdesk sample types)

**Out (this iteration):**

- Helpdesk `PlaceOrder` / `ChargePayment` / `OrderSaga` sample and Helpdesk.Tests
- `DoNotAssertOnExceptionsDetected` (wait does not collect-then-assert exceptions)
- Intercepting in-handler `IMessageBus.Publish` / `Invoke` (handlers return messages; those calls stay fire-and-forget)
- Production wait: host poller, `IHost.TrackActivity`, Tracking as the way production delayed work runs
- LocalQueues workers, back-pressure, drain-on-stop
- Looping `PlayDue` until the hold is empty at `asOf`
- `IClock`, `Task.Delay`, `Thread.Sleep` as the wait
- xUnit fixtures, FluentAssertions, Testcontainers inside Application
- Changing Scheduling snapshot-then-stop, `PlayDueInProgress`, or delayed-Invoke named errors
- Open recorder that accumulates several Invoke/Publish before one wait
- Catch-all `catch (Exception)` in the drain

**Deferred (named follow-up):**

- Helpdesk teaching conversation and the 12-month TrackActivity acceptance (timeout `NotFound` after complete)
- LocalQueues: untracked Publish / immediate cascades run off the tracked caller’s thread
- Hosting: poll `PlayDue(UtcNow)`; still must not use TrackActivity as the production wait
- `DoNotAssertOnExceptionsDetected` if a later slice wants collect-and-assert
- Bus intercept of in-handler Publish, or ConversationId-filtered drain
- Persistence: durable hold; session bags stay in-memory test state

---

## 3. Constraints

- Onion: Application owns the tracked session; Domain unchanged; adapters stay out. Core still must not reference test packages.
- Existing contracts stay: `ICascadePublisher` does not Invoke **except** that a tracked until-quiet drain consumes that publisher’s immediate list as Execution work. Untracked Invoke/Publish behavior is unchanged.
- `PlayDue` remains Scheduling’s API. Tracking does not become the production wait (already on `TrackingPlan`).
- No source generators. Packages 0.x.
- No catch-all. `OperationCanceledException` keeps Execution’s rule: bubble, no `HandlerFault`, no poison.
- Retry-now / `RetryWithCooldown` stay inside Execution’s attempt loop; each attempt is one Executed record.
- `ScheduleRetry` and `[Timeout]` stay parked; they are not immediate descendants.
- Completeness bias: a small complete session (drain + bags + one play snapshot) beats a Wolverine-shaped assertion DSL.
- In-memory only; process death loses the session (expected).
- Delayed `InvokeAsync` remains a named error (Scheduling). Tracking does not add delayed Invoke.

---

## 4. Decisions

| Decision | Choice | Rejected / why |
|----------|--------|----------------|
| Slice ambition | Test-time conversation runner | Recorder-only (cascades never execute); Helpdesk this slice; PlayDue alias only |
| Quiet | Until-quiet on cascade returns | One hop; drain in-handler `bus.Publish`; ConversationId filter |
| Tracked Publish | Root runs through Execution, then until-quiet | Record-only Publish (would not prove a published conversation) |
| PlayScheduled | One `PlayDue(asOf)` + until-quiet; new play-only session | Loop until hold empty at `asOf`; tests call `PlayDue` themselves; hide `asOf` / add `IClock` |
| Descendant faults | Fail-fast; throw exposes session | Wolverine collect + `DoNotAssertOnExceptionsDetected`; record-and-continue |
| Assertion model | Three bags; Executed per attempt | Per-envelope executed; ordered log only |
| Session start | One-shot tracked Invoke/Publish | Open recorder; `IHost.TrackActivity` (Hosting later) |
| Descendants | Cascade returns + PlayDue roots | Intercept all bus traffic while the session is live |
| Premise | HOLD this size | Shrink (no drain); expand (Helpdesk types) |
| Complexity | Proceed as-is (tracked Invoke **and** Publish) | Invoke-only this slice; collapse nested+overlapping |
| PlayDue wiring | Mediator owns `MessageScheduler` (same hold/executor/dispatcher). Session play delegates to it | Tests thread a hold into two objects; `PlayDue` on `IMessageBus` |
| Drain path | Untracked Mediator dispatch (saga load/NotFound). Not Executor-only like `PlayDue` | Copy `MessageScheduler.InvokeDue`; change `PlayDue` this slice |
| InvocationKind | Drain + tracked Publish root: `handler with { Scheduled = true }` | All Invoke-kind (cascaded `ScheduleRetry` would throw) |
| Envelope ids | Drain/tracked-Publish inherit parent ConversationId/CorrelationId | Mint new ids per descendant; kernel-wide assignment this slice |
| Fail-fast type | `HandlerFault.Session` nullable | `TrackingFault` wrapper (breaks `catch (HandlerFault)`); drop session |
| Attempt hook | `IHandlerAttemptObserver` last Executor param (after `scheduled`) | Session middleware; static `SeenAttempts` only |
| Public API | `InvokeTrackedAsync` / `PublishTrackedAsync` on `IMessageBus`; sealed `TrackedSession` | `TrackActivity()` builder; tracking only on `Mediator` concrete |
| Session bags | Three `IReadOnlyList` records; tests LINQ | Count/single helpers; `ITrackedSession` + FindSingle DSL |
| Named errors | `TrackInProgress`; `NestedTrackNotSupported` | One type for both; Wolverine exception names |
| Cascade cap | None | Named N; time budget |
| Tests | Full AC matrix in `MiniVerine.Tests` | Prove-with pair only |

---

## 5. Behavior & Flows

**Untracked path (unchanged)**

```text
Invoke → Execution → immediate cascades → ICascadePublisher.Publish (no Invoke)
                      delayed cascades  → hold
Publish             → accept, return; handlers do not run
PlayDue(asOf)       → snapshot hold → Execution each due envelope
```

**Tracked Invoke / tracked Publish**

```text
tracked root
  → Execution (Publish-in-session runs the root; Invoke already did)
  → immediate cascade bodies enqueue as the until-quiet worklist
  → each worklist item: Execution → more immediate cascades append; delayed park to hold
  → stop when the worklist is empty
  → return session
```

```mermaid
sequenceDiagram
    participant T as Test
    participant B as Bus
    participant E as Execution
    participant H as Hold

    T->>B: tracked Invoke(Start)
    B->>E: Start
    E-->>B: cascade Charge (immediate), Timeout (delayed)
    B->>H: park Timeout
    B->>E: Charge attempt 1 (throws)
    B->>E: Charge attempt 2 (throws)
    B->>E: Charge attempt 3 (ok, cascade Paid)
    B->>E: Paid
    B-->>T: session (Executed: Start, Charge x3, Paid; Scheduled: Timeout)

    T->>B: PlayScheduled(asOf = SentAt+1m)
    B->>H: PlayDue snapshot
    B->>E: Timeout
    B-->>T: new session (Executed: Timeout)
```

**Bags**

- **Executed:** one record per handler attempt (success or throw), including retry-now / cooldown attempts inside Execution. Root Invoke is Executed, not Published. Tracked Publish root is both Published (accepted) and Executed (drained).
- **Published:** outgoing **immediate** bodies accepted this session (cascades that did not park). Not the delayed ones.
- **Scheduled:** envelopes parked this session (`[Timeout]`, explicit delay, `ScheduleRetry`). Same envelope may later Executed on a play session.

**PlayScheduledMessagesAsync**

- Requires a session (continuation). Calls `PlayDue(asOf)` once, then until-quiet on immediate cascades from those roots.
- Newly parked work with `DeliverBy <= asOf` stays in the hold (Scheduling snapshot-then-stop).
- Returns a **new** session; bags are this play only. The previous session object is unchanged.
- Empty snapshot: success, empty bags on the new session.

**Faults and cancel**

- Root Invoke/Publish: today’s contract (`HandlerFault` after retries exhaust, etc.). `HandlerFault.Session` is set when the throw comes from a tracked wait.
- First descendant `HandlerFault`: stop the worklist; do not run remaining immediate siblings; throw that `HandlerFault` with `Session` set.
- `OperationCanceledException`: bubble; no poison; remaining worklist items are not drained; hold behavior for an in-flight PlayDue envelope follows Scheduling.
- Infinite immediate cascades: no cap. Cancel is the stop.

**Overlapping drains**

- Overlapping `PlayDue` remains `PlayDueInProgress`.
- Overlapping tracked until-quiet on the same bus throws `TrackInProgress`. Sequential tracked calls are allowed.
- Nested tracked Invoke from inside a handler throws `NestedTrackNotSupported`.

**What tests do not get**

- Production code calling tracked Invoke as a wait.
- Auto-fail at end of wait because some attempt threw and then retried to success (those throws are Executed records, not a failed wait).
- A FluentAssertions extension pack in Application.

---

## 6. Acceptance Criteria

- WHEN a tracked Invoke succeeds and the handler returns immediate cascade bodies, THE SYSTEM SHALL run each of those bodies (and their nested immediate cascades) through Execution on the caller’s thread before returning the session.
- WHEN a tracked Publish is called with an immediate message, THE SYSTEM SHALL run that root through Execution, then until-quiet, then return the session. WHEN untracked `PublishAsync` is called, THE SYSTEM SHALL still return without running handlers.
- WHEN a nested immediate cascade exists, THE SYSTEM SHALL execute it in the same wait (until-quiet, not one hop).
- WHEN a handler returns a delayed / `[Timeout]` / `ScheduleRetry` envelope, THE SYSTEM SHALL park it, SHALL NOT execute it in that until-quiet, and SHALL record it in Scheduled.
- WHEN Execution retries the same envelope in-process (retry-now / cooldown) and attempts 1–2 throw then attempt 3 succeeds, THE SYSTEM SHALL record three Executed entries for that message type on the session.
- WHEN a successful charge attempt cascades a paid event, THE SYSTEM SHALL record that paid event as Published and Executed on the same session.
- WHEN `PlayScheduledMessagesAsync(asOf)` is called, THE SYSTEM SHALL invoke `PlayDue(asOf)` once, drain that snapshot’s immediate cascades until-quiet, and SHALL return a new session whose bags contain only that play.
- WHEN a timeout was parked on session 1, THE SYSTEM SHALL NOT show it as Executed on session 1. WHEN play `asOf` is on or after `DeliverBy`, THE SYSTEM SHALL show it as Executed on session 2.
- WHEN play parks further delayed work due at the same `asOf`, THE SYSTEM SHALL leave that work unexecuted until a later play.
- WHEN a descendant handler exhausts retries and would `HandlerFault`, THE SYSTEM SHALL stop the drain, SHALL NOT execute remaining immediate siblings, SHALL throw `HandlerFault`, and SHALL set `HandlerFault.Session` to the session so far.
- WHEN the tracked root `HandlerFault`s, THE SYSTEM SHALL throw `HandlerFault` per today’s Invoke/Execution contract and SHALL set `HandlerFault.Session` with Executed attempts so far.
- WHEN until-quiet is cancelled, THE SYSTEM SHALL throw `OperationCanceledException`, SHALL NOT convert that to `HandlerFault`, and SHALL NOT drain remaining worklist items.
- WHEN a second tracked until-quiet is started on the same bus before the first returns, THE SYSTEM SHALL throw `TrackInProgress`.
- WHEN a handler starts a tracked session, THE SYSTEM SHALL throw `NestedTrackNotSupported` and SHALL NOT drain.
- WHEN delayed `InvokeAsync` is used, THE SYSTEM SHALL still throw the Scheduling delayed-Invoke named error (tracking does not add delayed Invoke).
- MiniVerine SHALL NOT `Task.Delay` or `Thread.Sleep` to wait for activity or `DeliverBy`.
- MiniVerine SHALL NOT require Helpdesk types, LocalQueues, Hosting, or `IHost.TrackActivity` to pass this slice’s tests.
- MiniVerine SHALL NOT intercept in-handler `IMessageBus.Publish` / `Invoke` for the drain.
- MiniVerine SHALL NOT loop `PlayDue` until the hold is empty.
- MiniVerine SHALL NOT make TrackActivity the production host’s wait.

**Prove-with (fixture, `MiniVerine.Tests`):**

1. Tracked Invoke of a start message that cascades a flaky charge (throws twice, succeeds, cascades paid) → session Executed counts: start 1, charge 3, paid 1; Scheduled contains the timeout companion if the start also returns one.
2. That timeout is not Executed until `PlayScheduledMessagesAsync(asOf)` on the returned session; the new session Executed contains the timeout once.

---

## 7. Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Tracking becomes a hidden LocalQueues | Untracked Publish still does not run handlers; drain is session-scoped, caller thread, cascade-only |
| Tests use tracked Invoke in production | Out of scope by name; Hosting poller uses `PlayDue`, not TrackActivity |
| Fail-fast throw loses the session | `HandlerFault.Session` set on tracked waits |
| `asOf` far in the future + loop would storm | One snapshot; tests that want the chain call play again |
| In-handler `bus.Publish` silently not drained | Documented; handlers return messages; intercept is deferred |
| Wolverine-shaped assertion catalog creep | Three bags only; no `DoNotAssertOnExceptionsDetected` this slice |
| Helpdesk teaching gap | Fixture conversation in kernel tests; Helpdesk deferred by name (same pattern as Sagas) |
| Infinite cascade hangs CI | No cap; cancel or CI timeout |
| Overlapping drain corrupts bags | `TrackInProgress`; `PlayDueInProgress` unchanged |

---

## 8. Rollout & Observability

- No feature flag; 0.x additive tracked API + session
- No Hosting auto-registration required; tests compose the bus they already use for Invoke
- Remove `TrackingPlan` when the session and tracked Invoke/Publish exist
- No new metrics this slice (Observability later)
- Logs: follow kernel — message type, envelope/conversation/correlation/saga ids, attempts, named error. No payload. Session bags are test state, not log output.

---

## 9. Open Questions

None — eng review locked the former table (no cascade cap; `InvokeTrackedAsync` / `PublishTrackedAsync`; `HandlerFault.Session`; inherit ids on drain only; Helpdesk timeout `NotFound` stay deferred).

Kernel-year ConversationId assignment on **public** `InvokeAsync` remains a later slice. Tracked roots still use today’s `EnvelopeForInvoke` mint; descendants inherit from that root.

## Implementation Tasks

- [ ] **T1 (P1)** — Executor attempt observer + `HandlerFault.Session`
  - Surfaced by: architecture — per-attempt bags; fail-fast must not wrap `HandlerFault`
  - Files: `src/MiniVerine/Application/Execution/IHandlerAttemptObserver.cs`, `Executor.cs`, `HandlerFault.cs`, `tests/MiniVerine.Tests/Application/Execution/ExecutorTests.cs`
  - Verify: observer sees attempts 1–2–3 on flaky retry-now; `new Executor(policies, scheduled: hold)` still binds the hold; untracked `HandlerFault.Session` is null

- [ ] **T2 (P1)** — `TrackedSession` bags
  - Surfaced by: code quality — sealed lists, no finder DSL
  - Files: `src/MiniVerine/Application/Tracking/TrackedSession.cs`
  - Verify: Published / Executed / Scheduled are `IReadOnlyList` records (message, envelope, attempt, exception)

- [ ] **T3 (P1)** — Track flag + named errors
  - Surfaced by: spec overlapping/nested; reuse `PlayDueInProgress` pattern
  - Files: `TrackInProgress.cs`, `NestedTrackNotSupported.cs`, `Mediator.cs`
  - Verify: overlapping `InvokeTrackedAsync` throws `TrackInProgress`; handler that starts a tracked call throws `NestedTrackNotSupported` and does not drain

- [ ] **T4 (P1)** — `InvokeTrackedAsync` until-quiet
  - Surfaced by: architecture — Mediator dispatch, Scheduled kind, inherit conversation/correlation ids
  - Files: `IMessageBus.cs`, `Mediator.cs`
  - Verify: nested immediate cascades execute; delayed stay Scheduled; untracked `InvokeAsync` still does not run cascades (CRITICAL); ConversationId matches the root on drained descendants

- [ ] **T5 (P1)** — `PublishTrackedAsync`
  - Surfaced by: proceed-as-is tracked Publish; `PublishAsync` still throws for untracked immediate
  - Files: `IMessageBus.cs`, `Mediator.cs`
  - Verify: immediate tracked Publish runs root then until-quiet (Scheduled kind); delayed tracked Publish parks only; untracked `PublishAsync` unchanged

- [ ] **T6 (P1)** — Mediator-owned `MessageScheduler` + `PlayScheduledMessagesAsync`
  - Surfaced by: architecture — session play must see Mediator’s hold
  - Files: `Mediator.cs`, `TrackedSession.cs`
  - Verify: `new Mediator(catalog)` tracked Invoke then play executes a `[Timeout]` companion; play returns a **new** session; newly parked due work waits for the next play; empty play succeeds with empty bags

- [ ] **T7 (P1)** — Full AC tests
  - Surfaced by: test review — not prove-with-only
  - Files: `tests/MiniVerine.Tests/Application/Tracking/TrackedSessionTests.cs`
  - Verify: `dotnet test` — prove-with pair; fail-fast + `Session`; cancel; empty play; cascaded `ScheduleRetry` parks; saga Handle cascade loads the same instance

- [ ] **T8 (P2)** — README + remove `TrackingPlan`
  - Surfaced by: spec rollout
  - Files: `README.md`, `src/MiniVerine/Application/Tracking/TrackingPlan.cs`
  - Verify: Tracking is no longer Plan-only; prove-with described without claiming Helpdesk types

## STRYDER REVIEW REPORT

| Review | Status | Findings |
|--------|--------|----------|
| Eng    | Cleared | Complexity proceed-as-is; Mediator owns scheduler; drain via Mediator Scheduled-kind; `HandlerFault.Session`; observer last; Invoke/PublishTrackedAsync; full AC tests; no cascade cap |

**VERDICT:** ENG CLEARED — ready to implement

**UNRESOLVED DECISIONS:** NO UNRESOLVED DECISIONS
