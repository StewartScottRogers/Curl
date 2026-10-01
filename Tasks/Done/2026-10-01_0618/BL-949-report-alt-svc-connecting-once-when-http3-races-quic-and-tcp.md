---
id: BL-949
title: Report Alt-svc connecting once when --http3 races QUIC and TCP to an alternative
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-733]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-949 — Report Alt-svc connecting once when --http3 races QUIC and TCP to an alternative

## Goal

When `--http3` races QUIC against TCP to an Alt-Svc alternative, the verbose line `* Alt-svc connecting from [...] to [...]` is printed once per transfer, not once per attempt.

## Context

- Measured (curl.se 8.18.0 build with ngtcp2, Windows, 2026-09-29; see BL-733's Notes and ADR-0226): `--http3 --alt-svc f` with the entry `h1 127.0.0.1 18736 h1 127.0.0.1 18735` prints `* Alt-svc connecting from [h1]127.0.0.1:18736 to [h1]127.0.0.1:18735` once, then the QUIC attempt, its failure, and the TCP attempt to the same alternative.
- `Curl.Networking.UnitLibrary/TcpConnector.cs` reports that line in `DestinationOf(ConnectTarget)`, which both `ConnectMultiplexedAsync` (through `ResolveForQuicAsync`) and `ConnectAsync` call, so a race prints it twice.
- Fix it in `TcpConnector` if the connector can tell the second call belongs to the same transfer; otherwise in `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, which runs the race (both are in `touches`).

## Acceptance criteria

- [x] A test pins that a `--http3` race whose QUIC attempt to the alternative fails and whose TCP attempt connects reports `* Alt-svc connecting from [h1]127.0.0.1:18736 to [h1]127.0.0.1:18735` exactly once, before the QUIC attempt's lines.
- [x] Tests pin that the line is still printed exactly once for a TCP-only connect to an alternative and for an `--http3-only` connect to one.
- [x] `dotnet build <project> -warnaserror` is clean for every project changed, and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage for each library changed, with no method over complexity 10 or CRAP 30.

## Notes

- Fixed in `TcpConnector` (no change to `Curl.Protocol.Http.UnitLibrary`): `HttpProtocolHandler` builds one `ConnectTarget` per connect (`TargetOf(plan)` in `ConnectAndExchangeAsync`) and hands the same object to both attempts of a race, through `PoolingConnector` unchanged. So `DestinationOf` now reports the `Alt-svc connecting` line once per target object, tracked by reference in a `ConditionalWeakTable<ConnectTarget, object>` (thread-safe `TryAdd`, holds no target alive). A new transfer, retry or redirect builds a new target and reports the line again.
- QUIC-first race order holds: `ResolveForQuicAsync` calls `DestinationOf` before its first await, so the line precedes the QUIC attempt's lines; with `TriesTcpBeforeQuic` it precedes the TCP attempt's.
- Tests: `Curl.Networking.UnitTests/TcpConnectorQuicTests.AltSvc.cs` - race (QUIC fails, TCP connects, same target: line once, first, then `Trying 127.0.0.1:18735...`; failed without the fix), TCP-only once, `--http3-only` once, two targets twice. An IP-literal alternative prints no `Host ... was resolved.` line.
- Measure-CodeQuality: Curl.Networking.UnitLibrary 100% line, 100% branch, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --http3 races to an Alt-Svc alternative report Alt-svc connecting once per transfer
