---
id: BL-945
title: Report Alt-svc connecting once when --http3 races QUIC and TCP to an alternative
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-733]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-945 — Report Alt-svc connecting once when --http3 races QUIC and TCP to an alternative

## Goal

When `--http3` races QUIC against TCP to an Alt-Svc alternative, the verbose line `* Alt-svc connecting from [...] to [...]` is printed once per transfer, not once per attempt.

## Context

- Measured (curl.se 8.18.0 build with ngtcp2, Windows, 2026-09-29; see BL-733's Notes and ADR-0226): `--http3 --alt-svc f` with the entry `h1 127.0.0.1 18736 h1 127.0.0.1 18735` prints `* Alt-svc connecting from [h1]127.0.0.1:18736 to [h1]127.0.0.1:18735` once, then the QUIC attempt, its failure, and the TCP attempt to the same alternative.
- `Curl.Networking.UnitLibrary/TcpConnector.cs` reports that line in `DestinationOf(ConnectTarget)`, which both `ConnectMultiplexedAsync` (through `ResolveForQuicAsync`) and `ConnectAsync` call, so a race prints it twice.
- Fix it in `TcpConnector` if the connector can tell the second call belongs to the same transfer; otherwise in `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, which runs the race (both are in `touches`).

## Acceptance criteria

- [ ] A test pins that a `--http3` race whose QUIC attempt to the alternative fails and whose TCP attempt connects reports `* Alt-svc connecting from [h1]127.0.0.1:18736 to [h1]127.0.0.1:18735` exactly once, before the QUIC attempt's lines.
- [ ] Tests pin that the line is still printed exactly once for a TCP-only connect to an alternative and for an `--http3-only` connect to one.
- [ ] `dotnet build <project> -warnaserror` is clean for every project changed, and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and 100% branch coverage for each library changed, with no method over complexity 10 or CRAP 30.

## Notes

## Log

- 2026-09-29: Created.
