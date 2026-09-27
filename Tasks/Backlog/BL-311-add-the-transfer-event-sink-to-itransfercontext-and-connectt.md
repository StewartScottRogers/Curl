---
id: BL-311
title: Add the transfer event sink to ITransferContext and ConnectTarget in Curl.Protocol.Abstractions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-163]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-311 — Add the transfer event sink to ITransferContext and ConnectTarget in Curl.Protocol.Abstractions

## Goal

`ITransferContext` and `ConnectTarget` expose the transfer event sink ADR-0046 decides, both default to a do-nothing implementation, and every existing handler, connector and test compiles and passes unchanged.

## Context

BL-163 recorded ADR-0046 (`Documentation/Planning/Decisions/ADR-0046-the-transfer-context-carries-a-transfer-event-sink-for-v-and-trace.md`). Implement exactly the members, payloads and defaults its Decision names; if it and this task disagree, the ADR wins.

- New files in `Curl.Protocol.Abstractions.UnitLibrary/`, namespace `Curl.Protocol.Abstractions`: `ITransferEvents`, `NoTransferEvents` (sealed, `Instance`), and the sealed records `ConnectionOpenedEvent`, `ConnectionReusedEvent`, `TlsHandshakeEvent`, with XML doc comments like `ITransferContext.cs`.
- `ITransferContext.Events` with no default interface implementation; `TransferContext.Events` an `init` property defaulting to `NoTransferEvents.Instance`; `ConnectTarget.Events` an `init` property with the same default. If another class implements `ITransferContext` (grep `: ITransferContext`), it needs the member too.
- Copying `Events` across redirect hops in `RedirectFollower.NextHop` is not this task (it is in `Curl.Core`; see BL-310 for the `Progress` copy). Nothing reports events yet. No package may be added.

## Acceptance criteria

- [ ] The interface, members, records and defaults named in ADR-0046 exist in `Curl.Protocol.Abstractions.UnitLibrary` with exactly those names.
- [ ] Tests in `Curl.Protocol.Abstractions.UnitTests` pin that a `TransferContext` built with only `Url` and `Output`, and a `ConnectTarget` built with only host, port and TLS flag, return `NoTransferEvents.Instance`, and that an initialised sink is returned as given.
- [ ] A test calls every member of `NoTransferEvents` and pins that none throws.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green for the whole solution; no new test needs `TestCategory=Integration`; 100% line and branch coverage of `Curl.Protocol.Abstractions.UnitLibrary` holds.

## Notes

## Log

- 2026-09-26: Created.
