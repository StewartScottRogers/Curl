---
id: BL-721
title: Add the datagram channel, QUIC stream and HTTP/3 version contracts to Curl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-718]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-721 — Add the datagram channel, QUIC stream and HTTP/3 version contracts to Curl.Protocol.Abstractions

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` gains exactly the contracts BL-718's ADR names for HTTP/3: a datagram channel (send and receive UDP datagrams, local and remote endpoints), a multiplexed connection that opens bidirectional streams and accepts unidirectional ones (each a byte stream with its own end and reset), the connector method that yields one, and the `HttpVersionPreference` values for `--http3` and `--http3-only`; documented, with defaults that leave every existing handler unchanged.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Contracts land before the work that uses them: QUIC (BL-724 uses the datagram channel), Networking (BL-728 implements both), HTTP (BL-731 consumes streams). Design: BL-718's ADR; add nothing it does not name.
- Files: `Curl.Protocol.Abstractions.UnitLibrary/IConnection.cs`, `IConnector.cs`, `HttpVersionPreference.cs` and whatever new files the ADR lists. `Abstractions_References_Nothing` must still pass.

## Acceptance criteria

- [x] Each new type and member has XML docs naming its consumer; `Curl.Protocol.Abstractions.UnitTests` cover any defaults and the new enum values.
- [x] `ProtocolIsolationTests` still pass.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered exactly ADR-0144 section 2: `IDatagramChannel.LocalEndPoint` (default `null`), `IMultiplexedConnection`, `IMultiplexedStream`, `MultiplexedStreamResetException`, `MultiplexedConnectionFailedException`, `MultiplexedConnectResult`, the default `IConnector.ConnectMultiplexedAsync` (exit 7, `QUIC is not available on this connector`) and `HttpVersionPreference.Http3`/`Http3Only`.
- Choice: `Http3` and `Http3Only` are appended after `Http10` now (values 2 and 3), because BL-659 has not yet added HTTP/2 values; the ADR's "after BL-659's values" only fixed the order of appending, and nothing persists or depends on the numeric values. HTTP/2 values, when BL-659 needs them, append after `Http3Only`. `HttpVersionPreferenceTests` pins the current order.
- Choice: `MultiplexedConnectResult.Connected` takes `ConnectTimings?` (nullable), as `ConnectResult` does, so a test fake need not invent timings.
- Both new exceptions derive from `IOException`, as `OutputWriteFailedException` does, so a handler catching `IOException` still catches them. `MultiplexedConnectionFailedException` refuses `CurlExitCode.Ok`.
- Verified: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Abstractions 588 passed, `ProtocolIsolationTests` included); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Abstractions carries ADR-0144's HTTP/3 contracts: multiplexed QUIC connection and streams, ConnectMultiplexedAsync with a no-QUIC default, datagram LocalEndPoint, and Http3/Http3Only
