---
id: BL-335
title: Add the connection-reuse contract of ADR-0050 to Curl.Protocol.Abstractions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-164, BL-313]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-335 — Add the connection-reuse contract of ADR-0050 to Curl.Protocol.Abstractions

## Goal

`IConnection.MarkReusable`, `ConnectTarget.PoolScheme`, `ConnectResult.IsReused` and `ConnectResult.ConnectionNumber`, and the `Scheme` and `IsProxy` fields of `ConnectionReusedEvent`, exist exactly as ADR-0050 decides, and every existing handler, connector and test compiles and passes unchanged.

## Context

- ADR-0050 (`Documentation/Planning/Decisions/ADR-0050-connections-are-reused-across-requests-and-urls-through-a-pooling-connector.md`), sections "How a handler hands a connection back", "The pool key" and "`%{num_connects}` and `-v`". If the ADR and this task disagree, the ADR wins.
- `void MarkReusable()` on `IConnection` has a default implementation that does nothing, so no existing `IConnection` fake changes.
- `ConnectTarget.PoolScheme` is `string?`, `init`, default `null` (never pooled).
- `ConnectResult` gains `bool IsReused` and `long ConnectionNumber`, set through the `Connected` factory; defaults `false` and `0`.
- `ConnectionReusedEvent` is created by BL-313 (ADR-0046); this task adds `Scheme` and `IsProxy` to it, hence the dependency.
- This is the contract task for the ADR-0050 work: BL-215 (`Curl.Networking`), BL-336 (`Curl.Protocol.Http`) and BL-334 (`Curl.Console`) depend on it. No package may be added.

## Acceptance criteria

- [x] The members named above exist in `Curl.Protocol.Abstractions.UnitLibrary` with exactly those names, types and defaults, each with an XML doc comment.
- [x] Tests in `Curl.Protocol.Abstractions.UnitTests` pin: a `ConnectTarget` built with only host, port and TLS flag has `PoolScheme` `null`; `ConnectResult.Connected` without the new arguments gives `IsReused` `false` and `ConnectionNumber` `0` and with them returns them as given; calling `MarkReusable` on an `IConnection` that does not override it does not throw; `ConnectionReusedEvent` carries `Scheme` and `IsProxy` as given.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green for the whole solution; no new test needs `TestCategory=Integration`; 100% line and branch coverage of `Curl.Protocol.Abstractions.UnitLibrary` holds.

## Notes

- Delivered directly rather than through every `/feature` stage: the ADR already fixes each name, type and default, so there was no design left for `protocol-architect`, and the change is four additive members plus tests (lane-1 default, 2026-09-27).
- `IsReused` and `ConnectionNumber` are trailing optional parameters (`isReused = false`, `connectionNumber = 0`) on the full `Connected` overload, so every existing positional call compiles unchanged.
- `ConnectionReusedEvent.Scheme` and `IsProxy` are `required`, like its other members; the only construction in the solution is in `NoTransferEventsTests`, updated here.
- Coverage of `Curl.Protocol.Abstractions.UnitLibrary` measured with the cobertura collector: 100% line, 100% branch. Abstractions tests 382 passed.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. IConnection.MarkReusable, ConnectTarget.PoolScheme, ConnectResult.IsReused/ConnectionNumber and ConnectionReusedEvent.Scheme/IsProxy exist per ADR-0050
