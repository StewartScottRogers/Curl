---
id: BL-332
title: Add the connection-reuse contract of ADR-0050 to Curl.Protocol.Abstractions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-164, BL-313]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-332 — Add the connection-reuse contract of ADR-0050 to Curl.Protocol.Abstractions

## Goal

`IConnection.MarkReusable`, `ConnectTarget.PoolScheme`, `ConnectResult.IsReused` and `ConnectResult.ConnectionNumber`, and the `Scheme` and `IsProxy` fields of `ConnectionReusedEvent`, exist exactly as ADR-0050 decides, and every existing handler, connector and test compiles and passes unchanged.

## Context

- ADR-0050 (`Documentation/Planning/Decisions/ADR-0050-connections-are-reused-across-requests-and-urls-through-a-pooling-connector.md`), sections "How a handler hands a connection back", "The pool key" and "`%{num_connects}` and `-v`". If the ADR and this task disagree, the ADR wins.
- `void MarkReusable()` on `IConnection` has a default implementation that does nothing, so no existing `IConnection` fake changes.
- `ConnectTarget.PoolScheme` is `string?`, `init`, default `null` (never pooled).
- `ConnectResult` gains `bool IsReused` and `long ConnectionNumber`, set through the `Connected` factory; defaults `false` and `0`.
- `ConnectionReusedEvent` is created by BL-313 (ADR-0046); this task adds `Scheme` and `IsProxy` to it, hence the dependency.
- This is the contract task for the ADR-0050 work: BL-215 (`Curl.Networking`), BL-333 (`Curl.Protocol.Http`) and BL-334 (`Curl.Console`) depend on it. No package may be added.

## Acceptance criteria

- [ ] The members named above exist in `Curl.Protocol.Abstractions.UnitLibrary` with exactly those names, types and defaults, each with an XML doc comment.
- [ ] Tests in `Curl.Protocol.Abstractions.UnitTests` pin: a `ConnectTarget` built with only host, port and TLS flag has `PoolScheme` `null`; `ConnectResult.Connected` without the new arguments gives `IsReused` `false` and `ConnectionNumber` `0` and with them returns them as given; calling `MarkReusable` on an `IConnection` that does not override it does not throw; `ConnectionReusedEvent` carries `Scheme` and `IsProxy` as given.
- [ ] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green for the whole solution; no new test needs `TestCategory=Integration`; 100% line and branch coverage of `Curl.Protocol.Abstractions.UnitLibrary` holds.

## Notes

## Log

- 2026-09-27: Created.
