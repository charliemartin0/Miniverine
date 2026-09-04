# Spec amendment: Message scheduling and operations dashboard

Approved 2026-09-04. This document amends the locked 12-month kernel spec. It supersedes that spec's exclusions for an admin SPA and its treatment of recurring scheduling as merely optional inspiration.

## 1. Outcomes & Why

AsyncMonolith currently owns scheduled work for StoreboostServices, while MiniVerine's operator story stops at telemetry and poison APIs. By month 12, MiniVerine should provide a message-native scheduling and operations experience inspired by TickerQ without becoming a general background-job framework or adopting its source-generated function model.

Success means one production SBS recurring workflow:

- is declared, persisted, published, monitored, and controlled through MiniVerine;
- survives process and node restarts;
- can be diagnosed and recovered without direct database intervention;
- runs while AsyncMonolith remains available for unmigrated workflows.

TickerQ is product inspiration for scheduling ergonomics and operator visibility, not a compatibility or feature-parity target.

## 2. Scope

**In:**

- Durable one-off publication of a typed message at a future time.
- Durable recurring publication of typed messages.
- Stable schedule and occurrence identities.
- At-least-once publication with deduplication information.
- Per-schedule missed-occurrence policy, defaulting to one coalesced publication.
- UTC schedules by default, with optional explicit civil-time zones.
- Application-declared, named recurring schedules.
- Persisted operator timing overrides.
- An official, opt-in dashboard served by the consuming host.
- Viewer and operator permissions using host-provided authentication.
- Schedule status/history and message-processing health.
- Operator actions: pause, resume, run now, edit timing, reset an override, replay poison, and discard poison.
- Metadata-only envelope inspection by default, with explicit host-provided safe summaries.
- A Clean Architecture guide and layered sample showing Domain, Application, Infrastructure, and Host responsibilities.
- A documented SBS cutover path for one recurring workflow.

**Out this iteration:**

- Arbitrary background-job functions.
- Source-generated job registration.
- TickerQ API compatibility or feature parity.
- Creating arbitrary message payloads from the dashboard.
- A dashboard dependency in the kernel.
- Built-in usernames or passwords.
- Automatic AsyncMonolith data migration, compatibility facade, or dual-run framework.
- Migrating every SBS schedule within 12 months.
- Storeboost-specific types or assumptions in MiniVerine.
- Enforcing a particular project or folder structure.
- Central orchestration across unrelated applications.
- General workflow chaining beyond normal MiniVerine message cascades and sagas.

**Deferred:**

- Reusable AsyncMonolith import tooling, justified only after the first production cutover.
- Multi-application operations hub.
- Additional dashboard authoring features based on operator evidence.
- External scheduler compatibility layers.

## 3. Constraints

- Scheduling remains message-native: a due occurrence publishes an envelope through normal MiniVerine routing and execution.
- The kernel remains usable without scheduling UI or web dependencies.
- Normal inbox, outbox, lease, retry, poison, correlation, and observability rules apply.
- A crash may cause duplicate delivery but must not silently lose a durably accepted occurrence.
- Each occurrence exposes a stable identity consumers can use for deduplication.
- Message bodies are not logged or displayed by default.
- Hosts own identity and authentication; MiniVerine defines viewer/operator authorization boundaries.
- Operational mutations are attributable to the authenticated actor.
- Domain and Application projects do not need to reference the dashboard package.
- Existing inspectable-kernel, no-codegen, ports-first, and production-persistence constraints remain in force.

## 4. Decisions

| Decision | Choice | Rejected / why |
|----------|--------|----------------|
| Scheduled unit | Typed message publication | Arbitrary functions would create a second execution model |
| Commitment | Scheduling and dashboard both ship within 12 months | Inspiration-only would not replace the SBS operational path |
| Dashboard boundary | Official opt-in host-served package | Core coupling would weaken the ignorable-adapter rule |
| Schedule ownership | Application-declared named schedules | Dashboard-only creation makes production behavior less reviewable |
| Operator authority | Timing controls plus poison recovery | Full payload authoring expands security and validation risk |
| Security | Host auth with viewer/operator separation | Built-in credentials duplicate host security |
| Delivery | At least once with stable occurrence ID | "Exactly once" cannot be promised across crashes and effects |
| Missed runs | Configurable; coalesce one by default | Always replaying all runs can create a post-outage storm |
| Time basis | UTC default; explicit zone optional | Host-local implicit time is deployment-dependent |
| Architecture | Neutral ports plus layered guidance/sample | Enforced layout would constrain consumers without improving runtime correctness |
| SBS migration | Capability plus cutover guide | Generic migration and dual-run machinery are premature |
| Payload visibility | Metadata plus opt-in safe summary | Automatic/full serialization risks leaking sensitive data |

## 5. Behavior & Flows

```text
Application declaration
  -> validate stable schedule identity and timing
  -> persist active definition or reconcile existing definition
  -> calculate next occurrence
  -> claim due occurrence on one node
  -> create envelope with schedule + occurrence identity
  -> durable Publish
  -> normal routing / handler / retry / poison lifecycle
  -> execution history and telemetry
  -> viewer observes; authorized operator intervenes
```

A recurring declaration defines what message is produced and its default timing. An operator override changes operational timing without rewriting application code. The dashboard must distinguish declared defaults from active overrides and allow an operator to return to the declared default.

After downtime, the schedule's policy determines whether MiniVerine skips missed occurrences, emits one coalesced occurrence, or catches up. Coalescing is the default. A manual "run now" creates an auditable occurrence without silently changing the next regular due time.

The dashboard presents schedules, next/previous occurrences, processing state, retries, poison reason, node/lease information where available, and safe operational metadata. Viewer access cannot mutate state. Every operator action records actor, time, target, and outcome.

The layered guidance assigns:

- Domain: messages and business rules without dashboard concerns.
- Application: handlers/use cases and declared scheduling intent.
- Infrastructure: durable scheduling and message-store adapters.
- Host: composition, dashboard exposure, authentication, and authorization.

## 6. Acceptance Criteria

- WHEN a one-off message is durably accepted for a future time, THE SYSTEM SHALL publish it no earlier than its due time and SHALL recover it after restart.
- WHEN a recurring occurrence becomes due, THE SYSTEM SHALL publish an envelope carrying stable schedule and occurrence identities.
- WHEN a scheduler node crashes around publication, THE SYSTEM SHALL preserve at-least-once delivery and expose enough identity to deduplicate a repeat.
- WHEN multiple nodes inspect the same due occurrence, THE SYSTEM SHALL prevent concurrent successful claims while recovering safely from a lost lease.
- WHEN downtime causes multiple missed occurrences, THE SYSTEM SHALL apply the schedule's configured policy and SHALL coalesce to one publication by default.
- WHEN no time zone is specified, THE SYSTEM SHALL interpret the schedule in UTC.
- WHEN an explicit time zone crosses a daylight-saving transition, THE SYSTEM SHALL apply documented deterministic behavior.
- WHEN application timing conflicts with a persisted operator override, THE SYSTEM SHALL identify the active override rather than silently replacing it.
- WHEN an operator resets an override, THE SYSTEM SHALL restore the application-declared timing.
- WHEN an operator selects run now, THE SYSTEM SHALL create an auditable manual occurrence without altering the next normal occurrence.
- WHEN a viewer accesses the dashboard, THE SYSTEM SHALL deny all state-changing operations.
- WHEN an authorized operator mutates a schedule or poison record, THE SYSTEM SHALL record the actor, action, target, time, and outcome.
- WHEN no safe summary is registered, THE SYSTEM SHALL display envelope metadata without displaying or logging its body.
- WHEN the dashboard package is absent, THE SYSTEM SHALL retain all non-dashboard scheduling behavior.
- WHEN the first SBS workflow is cut over, THE SYSTEM SHALL allow its remaining AsyncMonolith workflows to continue independently.

## 7. Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Dashboard overwhelms kernel scope | Optional package and stable operations boundary |
| Duplicate business effects | Stable occurrence identity and existing inbox/idempotency model |
| Outage causes publication storm | Coalescing default with explicit catch-up policy |
| Operator changes drift from code | Show declared default, active override, and reset action |
| Dashboard leaks sensitive data | Metadata-only default and host-authored safe summaries |
| Production controls are exposed incorrectly | Host auth, viewer/operator split, audited mutations |
| TickerQ inspiration becomes cloning | Explicit message-native boundary and rejected parity |
| SBS needs distort OSS API | SBS proves the path but owns migration-specific code |
| Civil-time behavior surprises users | UTC default and documented DST rules |

## 8. Rollout & Observability

- Prove behavior first in the layered Helpdesk sample, including restart, duplicate claim, missed occurrence, override, authorization, and poison recovery scenarios.
- Cut over one bounded SBS recurring workflow while AsyncMonolith continues serving the rest.
- Observe due-to-publish latency, publication outcomes, missed/coalesced counts, claim conflicts, retries, poison counts, operator actions, and active overrides.
- Do not expand to bulk SBS migration until the first workflow has an agreed observation period and rollback evidence.

## 9. Open Questions

| Question | Owner | Default if unresolved |
|----------|-------|-----------------------|
| Exact DST gap/overlap policy | Engineering review | Skip nonexistent local time; publish once for repeated local time |
| Declaration-versus-override reconciliation across deployments | Engineering review | Persist override until an operator explicitly resets it |
| Execution-history retention | Product + operations | Host-configurable, conservative finite retention |
| Live push versus periodic dashboard refresh | Engineering review | Correct periodic refresh; live push is optional |
| UI technology and asset delivery | Engineering review | Must remain replaceable and outside kernel |
| Safe-summary contract and redaction review | Security + engineering | No summary unless explicitly registered |
| First SBS workflow and rollback window | SBS owner | Small, low-risk recurring workflow with AsyncMonolith fallback |
| Accessibility target for first dashboard | Product | Keyboard-operable controls and readable status/error presentation |

This is intentionally narrower than TickerQ: it adopts the useful scheduling and operational outcomes while rejecting a second job-function model, source generation, payload authoring, and dashboard coupling.
