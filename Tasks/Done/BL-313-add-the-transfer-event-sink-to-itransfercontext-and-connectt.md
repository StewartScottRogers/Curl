---
id: BL-313
title: Add the transfer event sink to ITransferContext and ConnectTarget in Curl.Protocol.Abstractions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-163]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-313 — Add the transfer event sink to ITransferContext and ConnectTarget in Curl.Protocol.Abstractions

## Goal

`ITransferContext` and `ConnectTarget` expose the transfer event sink ADR-0046 decides, both default to a do-nothing implementation, and every existing handler, connector and test compiles and passes unchanged.

## Context

BL-163 recorded ADR-0046 (`Documentation/Planning/Decisions/ADR-0046-the-transfer-context-carries-a-transfer-event-sink-for-v-and-trace.md`). Implement exactly the members, payloads and defaults its Decision names; if it and this task disagree, the ADR wins.

- New files in `Curl.Protocol.Abstractions.UnitLibrary/`, namespace `Curl.Protocol.Abstractions`: `ITransferEvents`, `NoTransferEvents` (sealed, `Instance`), and the sealed records `ConnectionOpenedEvent`, `ConnectionReusedEvent`, `TlsHandshakeEvent`, with XML doc comments like `ITransferContext.cs`.
- `ITransferContext.Events` with no default interface implementation; `TransferContext.Events` an `init` property defaulting to `NoTransferEvents.Instance`; `ConnectTarget.Events` an `init` property with the same default. If another class implements `ITransferContext` (grep `: ITransferContext`), it needs the member too.
- Copying `Events` across redirect hops in `RedirectFollower.NextHop` is not this task (it is in `Curl.Core`; see BL-310 for the `Progress` copy). Nothing reports events yet. No package may be added.

## Acceptance criteria

- [x] The interface, members, records and defaults named in ADR-0046 exist in `Curl.Protocol.Abstractions.UnitLibrary` with exactly those names.
- [x] Tests in `Curl.Protocol.Abstractions.UnitTests` pin that a `TransferContext` built with only `Url` and `Output`, and a `ConnectTarget` built with only host, port and TLS flag, return `NoTransferEvents.Instance`, and that an initialised sink is returned as given.
- [x] A test calls every member of `NoTransferEvents` and pins that none throws.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green for the whole solution; no new test needs `TestCategory=Integration`; 100% line and branch coverage of `Curl.Protocol.Abstractions.UnitLibrary` holds.

## Notes

- Delivered in-session rather than through the full `/feature` stages: ADR-0046 already names every member, payload and default, so there was no design left to plan; the plan was the ADR's Decision, implemented verbatim.
- The event records use `required` init properties (the ADR says "sealed records with required members"), not positional parameters.
- `ConnectTarget.Events` takes part in record equality by reference; the default is the one `NoTransferEvents.Instance`, so two targets built without a sink stay equal.
- Tests use `StubTransferEvents`, a sink whose members throw, to pin that a given sink is returned as given; `NoTransferEventsTests` calls every `NoTransferEvents` member and round-trips each record.
- No other class implements `ITransferContext` (grep `: ITransferContext`), so nothing else changed. Nothing reports events yet; `RedirectFollower.NextHop` copying is left to its own task per the ADR.
- Verified: `dotnet build -warnaserror` clean, fast tests green solution-wide (Abstractions 377 passed), `Curl.Protocol.Abstractions.UnitLibrary` line and branch coverage 100%. `dotnet format --verify-no-changes` reports ENDOFLINE in `Curl.Protocol.Telnet.UnitTests/TelnetProtocolHandlerTests.cs`, outside this task's `touches`; left alone.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ITransferEvents, NoTransferEvents, the three event records, ITransferContext.Events and ConnectTarget.Events exist per ADR-0046, defaulting to the do-nothing sink
