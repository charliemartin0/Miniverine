# MiniVerine

A Wolverine-shaped in-process bus, built in slices. This repo sits next to `WolverineTest` (the real-Wolverine learning sample), not as a dependency.

**12-month product contract:** [specs/miniverine-12-month-kernel.md](specs/miniverine-12-month-kernel.md) — inspectable kernel (no source-gen), ports-first persistence, named errors. Rabbit/HTTP/cron are ignorable adapters. Do not treat this README’s slice list as permission to clone Wolverine’s full catalog.

Read the **code that exists**, then use the questions below. Answers are collapsed so you can try to explain each one yourself. Questions marked **(planned)** are in `*Plan` comments only — MiniVerine does not run that pipeline yet. Open `WolverineTest` when you want to see the finished conversation.

## What you should feel

Handlers never call each other. They return **messages**. The bus wraps each message in an **Envelope** and either runs it now (`Invoke`) or lets go (`Publish`). Time, retries, and “the payment never came” are messages too.

Postgres, Rabbit, and HTTP are adapters. Core must not know those packages. Domain has no I/O. Application has ports only. Infrastructure composes and implements.

```mermaid
sequenceDiagram
    participant Program
    participant Saga as OrderSaga
    participant Store as saga store
    participant Payments as payments queue
    participant Clock as scheduled timeout

    Note over Program,Clock: Target conversation. WolverineTest today. MiniVerine Application exists (Discovery, Mediator, Execution); sagas and queues are still planned.

    Program->>Saga: Publish PlaceOrder
    Saga->>Store: commit saga and ChargePayment envelope
    Saga-->>Payments: cascade ChargePayment
    Saga-->>Clock: cascade OrderTimeout at T plus 1m
    Payments->>Payments: fail, retry, succeed
    Payments->>Saga: cascade PaymentCharged
    Saga->>Store: MarkCompleted
    Clock->>Saga: OrderTimeout later
    Saga->>Saga: NotFound already done
```

## Layout

```
MiniVerine/
  src/MiniVerine/
    Domain/                      Envelope, wire names, saga identity, named errors — no I/O
    Application/                 Invoke, cascades, routing, execution, sagas — ports only
    Infrastructure/              host, JSON, local queues, transport/persistence ports
  src/MiniVerine.Postgresql/     persistence adapter (empty)
  src/MiniVerine.RabbitMQ/        broker adapter (empty)
  src/MiniVerine.Http/           HTTP front door (empty)
  samples/Helpdesk/src/
    Helpdesk.Domain/             entities / value objects — no MiniVerine, no infra
    Helpdesk.Application/       messages + handlers — Domain + MiniVerine
    Helpdesk.Infrastructure/      Marten/Npgsql wiring — Application + MiniVerine.Postgresql
    Helpdesk.Host/              composition root (Generic Host)
  tests/MiniVerine.Tests
  tests/Helpdesk.Tests
```

Each MiniVerine feature folder has a `*Plan` class. The XML comments are the spec: **Put here**, **Do not put here**, **Prove with**. Core must not reference the adapter projects. Npgsql stays in `MiniVerine.Postgresql`; Rabbit in `MiniVerine.RabbitMQ`; HTTP in `MiniVerine.Http`.

Helpdesk.Domain has no MiniVerine and no infra. Helpdesk.Application may reference MiniVerine. Helpdesk.Infrastructure may reference MiniVerine.Postgresql. Helpdesk.Host is the composition root.

## What is done

### Domain/Envelope

The wrapper that travels. `Envelope` plus value objects (id, destination, correlation, conversation, clocks, headers, content type, attempts, bytes) and FluentValidation. Envelope **carries** `Message`, `MessageType`, and `SagaId`; it does not own those types.

### Domain/Messaging

How a CLR type is named on the wire.

- `[MessageIdentity]` optional alias
- `MessageTypeNaming` — attribute wins, otherwise `FullName`
- `MessageTypeCatalog` — register, `GetName`, `Lookup`
- `KnownMessageType` / `UnknownMessageType` — unknown names are a result, not an exception
- `Message` / `MessageType` value objects and validators

### Domain/Sagas

Identity and timeout as data, not a running timer.

- `SagaId` (empty string = not part of a saga)
- `[SagaIdentity]` on a message property
- `SagaIdentityNaming` — `[SagaIdentity]`, then `{SagaType}Id`, then `Id`
- `[Timeout(Minutes = 1)]` delay metadata (`TimeSpan Delay`)
- `Saga` base — `MarkCompleted` / `IsCompleted` only; no `Id` on the base
- Validators for `SagaId` and `[Timeout]`

Prove-with for this folder: given a message, you can say which saga instance it belongs to without I/O.

### Domain/Errors

Named recovery vocabulary. `ErrorAction` (`Retry`, `RetryWithCooldown`, `MoveToErrorQueue`, `Requeue`, `ScheduleRetry`, `Discard`) and lookup results (`FoundErrorPolicy` / `MissingErrorPolicy`). Application/Execution owns the catalog, ports, and applying the chain.

- Validators for cooldown delay (≥ 0), non-empty found chains, and missing exception type
- Catalog registration does not run these validators yet
- `ValueObjects/` folders live under Domain only

### Infrastructure/Hosting (partial)

`UseMiniVerine()` and `MiniVerineOptions` exist. The sample host starts and stops with no messages. Listeners, drain-on-stop, and handler-assembly options are still in `HostingPlan`.

## What is left

Folders that are **Plan-only** are listed in a sensible build order. Do one slice at a time; prove it before starting the next.

### Application (the bus)

1. **Discovery** — find `Handle` / `HandleAsync` / `Consume` / `Start` by convention. No codegen yet.
2. **Bus** — `IMessageBus` (`InvokeAsync`, `PublishAsync`). Public facade; no threads or sockets.
3. **Mediator** — `InvokeAsync` on the caller’s thread until `Handle` returns.
4. **Cascades** — handler return values become outgoing messages after success. Failure publishes nothing.
5. **Routing** — message type → destination URI (`local://payments/`). Not the queue implementation.
6. **Execution** — wrap one handler call: attempts, retry policy, `IMissingHandler`.
7. **Middleware** — Russian doll around Execution. Same handler programming model.
8. **Scheduling** — time is a message. `[Timeout]` / delay → `Envelope.DeliverBy`. Fast-forward for tests. No `Task.Delay` on a saga.
9. **Application/Sagas** — load by id, run `Start` / `Handle` / `NotFound`. `ISagaStore` as a port. Domain owns identity; this folder owns the conversation.
10. **Tracking** — `TrackActivity`-shaped session for tests (`PlayScheduledMessagesAsync`).

### Infrastructure

11. **Hosting** — `IHostedService` starts listeners / durability agents and drains on `StopAsync`.
12. **Serialization** — Envelope body ↔ bytes using Domain/Messaging type names. Unknown CLR type is a handled failure.
13. **LocalQueues** — in-process queues that obey Routing destinations.
14. **Transports** — `ITransport` / endpoint ports (`local://`, later `tcp://`). Rabbit lives in `MiniVerine.RabbitMQ`.
15. **Persistence** — inbox / outbox / dead letter / saga store ports. In-memory first; Npgsql in `MiniVerine.Postgresql`.
16. **Observability** — OpenTelemetry exporters, not the Execution policies themselves.

### Adapters and sample

17. **MiniVerine.Postgresql** — Marten/Npgsql implementation of persistence ports.
18. **MiniVerine.RabbitMQ** — broker transport.
19. **MiniVerine.Http** — HTTP front door into the same Execution pipeline.
20. **Helpdesk sample** — `PlaceOrder` / `ChargePayment` / `OrderSaga` against MiniVerine, matching `WolverineTest`.
21. **Tests** — `MiniVerine.Tests` for domain (naming, catalog lookup, saga identity, envelope rules). `Helpdesk.Tests` for conversations once the bus exists.

## Start in this order

1. `src/MiniVerine/Domain/Envelope/Envelope.cs` — the unit of work
2. `src/MiniVerine/Domain/Messaging/MessageTypeNaming.cs` and `MessageTypeCatalog.cs` — wire names
3. `src/MiniVerine/Domain/Sagas/SagaIdentityNaming.cs`, `TimeoutAttribute.cs`, `Saga.cs` — identity and inert state
4. `src/MiniVerine/Domain/Envelope/Validators/EnvelopeValidator.cs` — composition, not ownership
5. Any `*Plan.cs` in Application — the next slices
6. `samples/Helpdesk/src/Helpdesk.Host/Program.cs` — host glue only
7. Sibling `WolverineTest` — the finished pipeline this clone is aiming at

## Build

```bash
dotnet build src/MiniVerine
dotnet run --project samples/Helpdesk/src/Helpdesk.Host
dotnet test
```

.NET 10. Tests and the Helpdesk sample compile; they do not exercise a bus yet.

## Questions

Open an answer only after you have a guess. If you can explain it to a rubber duck without opening the answer, you have the idea.

If you already know **MediatR**, **clean architecture**, and **DDD**, several questions are a translation into this codebase. Frontend stack is out of scope here.

### From the code that exists

<details>
<summary>What is a message in this project, versus a method call?</summary>

A message is a piece of data (a `record` such as `PlaceOrder`) that *might* be handled later, on another thread, after a retry, or after a process restart. MiniVerine wraps that body in `Message` (`Domain/Messaging/ValueObjects/Message.cs`): the CLR object, not JSON.

A method call is “run this now, on my stack, throw if it fails.” If `OrderSaga.Start` called `ChargePaymentHandler.Handle(...)` directly, you would skip the queue, the retry policy, the inbox, and the chance to load saga state again. Event-driven code decides *what happened* (or what should happen next) and emits a message. It does not reach into the next step.

Helpdesk does not contain those records yet. The types live in Messaging so Envelope can carry a body without owning “what a message is.”

</details>

<details>
<summary>What is an Envelope, and why isn’t the record enough?</summary>

Your `record` is the body. MiniVerine’s unit of work is `Envelope`: body plus `EnvelopeId`, `MessageType`, `Destination`, correlation / conversation / saga ids, `SentAt`, `DeliverBy`, `Headers`, `ContentType`, `Attempts`, and `EnvelopeData` (bytes, empty until Serialization).

Retries will reuse the **same** envelope (`Attempts` 1, then 2, then 3). They are not three new publishes. Conversation ids are why a future `TrackActivity` can wait for PlaceOrder + ChargePayment + PaymentCharged as one conversation. The inbox will store envelopes, not bare records, so a restart can continue the same attempt count.

See `src/MiniVerine/Domain/Envelope/Envelope.cs`.

</details>

<details>
<summary>Why do Message, MessageType, and SagaId live outside Envelope?</summary>

Envelope **carries** them. Messaging **defines** the wire name. Sagas **defines** instance identity.

If `MessageType` lived only on Envelope, Messaging would import Envelope for its own core idea (`MessageTypeNaming` returns a name). The wrapper would own the contract. After the move, Envelope depends on Messaging and Sagas. Messaging does not depend on Envelope.

`Message` is the CLR body slot. `MessageType` is the stable string on the wire. `SagaId` is which process-manager instance this envelope belongs to (empty if none). Destination, attempts, and bytes stay on Envelope: those are how *this copy* is sent, retried, and stored.

</details>

<details>
<summary>What does MessageTypeNaming do that the catalog does not?</summary>

`MessageTypeNaming.For(Type)` is a pure function: `[MessageIdentity]` alias if present, otherwise `FullName` (not `Name`, not `AssemblyQualifiedName`). It always has a `Type`, so this direction does not fail.

`MessageTypeCatalog` is the map Discovery will fill. `Register` records every `(Type, MessageType)` pair (collisions stay on the list for the validator). `_byType` is outbound `GetName`. `_byName` is inbound `Lookup`. Two processes that register the same CLR type must agree on the string.

A dictionary cannot hold two types with the same name. That is why the list is the source of truth.

</details>

<details>
<summary>Why is an unknown wire name a lookup result, not an exception?</summary>

A Rabbit payload or inbox row has a string, not a `Type`. `Lookup` returns `KnownMessageType` or `UnknownMessageType`. SerializationPlan: unknown CLR type is a handled failure handed to Execution / `IMissingHandler`, not a crash in the serializer.

`MessageTypeValidator` only says “not empty, not whitespace.” “We have never registered this name” is Messaging’s job, and it is allowed to fail in a typed way.

</details>

<details>
<summary>Why FullName, not Name or AssemblyQualifiedName?</summary>

`Name` alone (`PlaceOrder`) collides across namespaces. `AssemblyQualifiedName` includes the assembly version; bumping a package would change the wire name and break every stored envelope and every other process.

`FullName` is stable across versions. `[MessageIdentity("place-order")]` is the override when you rename the class but must keep the contract.

</details>

<details>
<summary>Why [SagaIdentity] on OrderId? Why not just a property named OrderId?</summary>

`SagaIdentityNaming` looks at **properties on the message**, not `OrderSaga.Id`. Order: `[SagaIdentity]`, then `{SagaType.Name}Id` (`OrderSaga` → `OrderSagaId`), then `Id`.

Helpdesk messages use `OrderId`. The saga class is `OrderSaga`, so the conventional name would be `OrderSagaId`. Without the attribute, `PlaceOrder` would not correlate.

`ChargePayment` also has `OrderId` and **no** `[SagaIdentity]`, no `OrderSagaId`, no `Id`. Naming returns empty `SagaId`. It is not a saga message. Empty means “not this saga,” not an error. `NotFound` is Application later.

Identity is a contract on the message, not a vibe. See `SagaIdentityNaming.For(object, Type)`.

</details>

<details>
<summary>Why is timeout an attribute, not TimeoutMessage or Task.Delay?</summary>

Wolverine uses `OrderTimeout : TimeoutMessage(1.Minutes())`. Attribute arguments cannot be `TimeSpan`, so MiniVerine uses `[Timeout(Minutes = 1)]` on the message type: integers, then `Delay` as `TimeSpan`.

The saga class is inert state (`MarkCompleted` / `IsCompleted`). It does not run a timer. Scheduling (planned) will set `Envelope.DeliverBy` from `SentAt + Delay` and deliver the envelope later like any other message.

`await Task.Delay(1.Minute())` inside `Start` would block a worker, die with the process, and be painful to test. `PlayScheduledMessagesAsync` (planned, Application/Tracking) fast-forwards the timeout without sleeping.

</details>

<details>
<summary>Why does Saga have no Id? Why is MarkCompleted only a flag?</summary>

`OrderSaga` will declare `public int? Id { get; set; }`. Other sagas will use `Guid` or `string`. An `Id` on the MiniVerine base would force one CLR type on every saga.

`MarkCompleted()` sets `IsCompleted`. It does not delete a Marten document. Application/Sagas will treat the flag as “this instance is finished”; Persistence will own the row. Domain/Sagas is data. Dispatch is not here.

</details>

<details>
<summary>Why FluentValidation on domain types instead of throwing in constructors?</summary>

Envelope value objects are records. They accept values; validators reject bad combinations (`DeliverBy >= SentAt`, ContentType paired with Data, empty `SagaId` allowed). Messaging does not throw on an unknown name. `[Timeout]` with all zeros fails `TimeoutAttributeValidator`, not the attribute constructor.

Application will run validators at catalog build and at envelope construction. The edge of the bus should see a validation result, not an unhandled `ArgumentException` from a record initializer.

`EnvelopeValidator` composes Messaging and Sagas child validators. Ownership of the rules follows ownership of the types.

</details>

<details>
<summary>What is a *Plan class? Why empty sealed types with comments?</summary>

Each feature folder’s `*Plan` is the spec for a slice that may not have runtime types yet: **Put here**, **Do not put here**, **Prove with**. Domain, Discovery, Bus, Mediator, and Cascades used to be Plan-only; they now have real types. Application/Middleware is still a Plan.

The empty `sealed class` exists so the folder is a compilable C# project, not a markdown wiki. Implement against the comments. Do not invent a different split.

</details>

<details>
<summary>How does this sit with clean architecture (onion) and DDD?</summary>

Inner layers do not depend on outer ones. Domain does not know HTTP or Postgres. Application orchestrates. Infrastructure implements ports. Adapters (`MiniVerine.Postgresql`, `.RabbitMQ`, `.Http`) are separate projects so core cannot take those package references.

- **Messages** are the language of the domain. They are not a controller DTO and not a SQL row.
- **Handlers / saga methods** (planned in Helpdesk.Application) are use cases. `OrderSaga.Start` must not new up `ChargePaymentHandler`.
- **Helpdesk.Host** is the composition root. Helpdesk.Domain must not reference MiniVerine.

A **saga is a process manager**, not an aggregate. MiniVerine’s `Saga` does not enforce “an order’s line items.” It tracks “this instance is open until something calls `MarkCompleted`.” The timeout is a message in time, not a `DateTime` field you poll.

This repo is not a full domain model with repositories. The lesson is the messaging shape that DDD + onion usually want, plus a bus you own one folder at a time.

</details>

### The bus that is not built yet (planned)

<details>
<summary>(planned) Invoke vs Publish — what is the difference?</summary>

Both will end up in Execution (envelope, handler, error policy, cascades). They differ in **when the caller continues**. See `Application/Bus/IMessageBus.cs` and `Application/Mediator/Mediator.cs`.

**`InvokeAsync`** is a function call through the bus. `await` does not finish until that handler returns. Retry-now / retry-with-cooldown happen inside that await. There is no queue in front of the caller. Use Invoke when the current request cannot continue until *this* handler has finished.

**`PublishAsync`** is “accept this envelope and let go.” Routing runs, the message lands on a local queue, and the caller is done.

Rule of thumb: **Invoke = I need this handler done before I continue. Publish = I need this work to happen, not necessarily here or now.**

`IMessageBus` does not own threads or sockets. Mediator vs Routing/LocalQueues implement the difference.

</details>

<details>
<summary>(planned) How does this compare to MediatR?</summary>

**`InvokeAsync` is MediatR’s `Send`.** One message in, handler runs now, caller waits. MiniVerine will find `Handle(PlaceOrder)` by convention (`Application/Discovery`) instead of `IRequestHandler<PlaceOrder>`.

**`PublishAsync` is not MediatR.** The work can retry, land on another thread, or survive a restart. MediatR `INotification` still is not a queue, an outbox, or a saga.

The trap is to `InvokeAsync` the next step from inside a handler (`IMediator.Send` the next command). That keeps you on one stack, with stale saga state, and no outbox. Return `ChargePayment` and let the bus load `OrderSaga` again (`Application/Cascades`).

| You know | In MiniVerine (planned) |
| --- | --- |
| `IMediator.Send` / `IRequestHandler<T>` | `InvokeAsync` |
| `IMediator.Publish` / `INotification` | still in-process; not a durable queue |
| “controller calls the next handler” | cascade a message instead |
| One handler, one HTTP request | `PlaceOrder` starts a conversation that outlives the call |

</details>

<details>
<summary>(planned) Why do cascading messages wait for the handler to succeed?</summary>

A cascade is a return value the bus will publish **after** the current handler succeeds. `OrderSaga.Start` will return `(saga, OrderTimeout, ChargePayment)`. That is not a call to the payment handler.

If the handler throws, nothing is emitted. That is the in-memory **outbox** (`Application/Cascades`). Durable outbox is the same rule persisted (`Infrastructure/Persistence`). Do not `InvokeAsync` the next saga step from inside a saga handler.

Prove-with: a throwing handler publishes nothing; a succeeding handler publishes exactly its return values, after it returns. See `CascadingMessages` and `ICascadePublisher`.

</details>

<details>
<summary>(planned) Why does the outbox exist? How is that different from the inbox?</summary>

The dual-write bug is: write business state, then publish, and crash between the two. Or publish, then fail to save.

**Outbox:** outgoing envelopes are rows in the same transaction as the saga. Crash after commit and the envelope is still there.

**Inbox:** write the incoming envelope *before* the handler runs; mark handled only after success. Kill the host mid-retry and the same envelope is recovered.

Outbox: I will not forget work I decided to emit. Inbox: I will not forget work that arrived. Ports live in `Infrastructure/Persistence`; Npgsql in `MiniVerine.Postgresql`.

</details>

<details>
<summary>(planned) What is a local queue? Why named payments?</summary>

With no RabbitMQ, the bus still has queues: in-process workers. Routing (`Application/Routing`) maps `ChargePayment` → `local://payments/`. LocalQueues (`Infrastructure/LocalQueues`) obey that destination. A worker runs the handler off the caller’s thread.

Routing is a table of rules, not a switch inside each handler. The TPL Dataflow block is Infrastructure. `ChargePayment` should get `local://payments/` without the saga naming a queue in `Start`.

</details>

<details>
<summary>(planned) Why configure retry on the handler instead of a for-loop?</summary>

Execution (`Application/Execution`) wraps one handler call: attempts, retry / retry-with-cooldown / requeue / dead-letter, by exception type and/or message type. `InvokeAsync` only applies retry and retry-with-cooldown (match Wolverine).

A `for` loop inside the handler would block one worker, mix “call the gateway” with “how we recover,” and skip dead-letter as a first-class outcome. The policy lives on the chain so every `ChargePayment` gets the same recovery, including inbox recovery after a restart.

The same `Envelope` comes back with `Attempts` 1, 2, 3; then error-queue if the policy says so.

</details>

<details>
<summary>(planned) What is a saga doing that a chain of cascades does not?</summary>

Cascades are “after this handler succeeds, emit these messages.” There is no state sitting around between them except whatever you put in the message bodies.

A **saga** is state that lives across several messages for one business process: “order 1 is open until paid or until it times out.” Domain already has identity and `MarkCompleted`. Application/Sagas will load by id, run `Start` / `Handle` / `NotFound`. Persistence will store the row.

Without a saga you could still cascade `ChargePayment`, but you would have nowhere honest to put “this order is still waiting.”

</details>

<details>
<summary>(planned) Why does NotFound exist? Isn’t that just an error?</summary>

After payment, `MarkCompleted()` finishes the saga. The timeout scheduled at start is **not cancelled**. A minute later it still arrives.

If there is no `NotFound(OrderTimeout)`, “saga 1 is gone” is a failure. `NotFound` means “this message is harmless because the other path already finished.” Same for `PaymentCharged` if the timeout already won.

That is normal in EDA: you design for messages that show up late, twice, or after the process is over. Prove-with on `SagasPlan`: after complete, timeout hits `NotFound` instead of failing.

</details>

<details>
<summary>(planned) Why is there no PlaceOrderHandler?</summary>

Once `OrderSaga.Start(PlaceOrder)` is wired with a real saga identity, `PlaceOrder` is the saga’s start message. A separate `Handle(PlaceOrder)` should not also run.

`Start` is the PlaceOrder handler. It cascades `ChargePayment` and the timeout message. One message in, one place that decides the next work. Discovery will treat `Start` as a handler convention.

</details>

<details>
<summary>What is this repo deliberately not doing yet?</summary>

No running bus, no RabbitMQ, no HTTP, no Postgres inbox. Helpdesk.Host only calls `UseMiniVerine()` and starts/stops. There are no MiniVerine.Tests for Envelope, Messaging, or Sagas yet.

The next slices are Application/Discovery through Tracking, then Serialization, LocalQueues, Persistence. External transport and HTTP come after the in-process conversation works. Until then, `WolverineTest` is the finished sample of the pipeline MiniVerine is cloning.

</details>
