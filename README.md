# MiniVerine

A Wolverine-shaped in-process bus, built in slices. This repo sits next to `WolverineTest` (the real-Wolverine learning sample), not as a dependency.

Handlers never call each other. They return **messages**. The bus wraps each message in an **Envelope** and either runs it now (`Invoke`) or lets go (`Publish`). Time, retries, and “the payment never came” are messages too. Postgres, Rabbit, and HTTP are adapters. Core must not know those packages.

## Layout

```
MiniVerine/
  src/MiniVerine/
    Domain/                      Envelope, wire names, saga identity — no I/O
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

Domain has no I/O. Application has ports only. Infrastructure composes and implements.

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

### Infrastructure/Hosting (partial)

`UseMiniVerine()` and `MiniVerineOptions` exist. The sample host starts and stops with no messages. Listeners, drain-on-stop, and handler-assembly options are still in `HostingPlan`.

## What is left

Folders that are **Plan-only** (comments, no runtime) are listed in a sensible build order. Do one slice at a time; prove it before starting the next.

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

## Build

```bash
dotnet build src/MiniVerine
dotnet run --project samples/Helpdesk/src/Helpdesk.Host
dotnet test
```

.NET 10. Tests and the Helpdesk sample compile; they do not exercise a bus yet.
