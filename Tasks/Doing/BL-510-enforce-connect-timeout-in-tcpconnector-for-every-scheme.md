---
id: BL-510
title: Enforce --connect-timeout in TcpConnector for every scheme
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-498]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-510 — Enforce --connect-timeout in TcpConnector for every scheme

## Goal

A TCP connect (and the TLS handshake, and a proxy tunnel, as curl counts them) that outlasts `--connect-timeout` (or the remaining `--max-time`) fails with exit 28 and curl 8.21.0's message for every scheme, as BL-498's ADR places it.

## Context

- Conformance audit 2026-09-28, row 12 (Blocker): `Curl.Networking.UnitLibrary/TcpConnector.cs` ignores `--connect-timeout`. The design is BL-498's ADR; read it first, including whether `ConnectTarget` (Abstractions) carries the deadline.
- Timings and failures: `ConnectResult`, `NumberedConnectFailure.cs`, `TlsFailureMessages.cs`; HTTP's own enforcement is ADR-0040.
- Inject `TimeProvider`; tests use a fake one and a dialer that never completes.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--connect-timeout 1` against a non-routable address (e.g. `10.255.255.1`) and against a loopback listener that accepts but never answers a TLS ClientHello (`https://`), with `-v`; stderr and exit code copied into Notes.
- [ ] `Curl.Networking.UnitTests` tests on a fake `TimeProvider` pin exit 28 and the measured message for a stalled dial and a stalled handshake, and that a connect finishing in time is unaffected.
- [ ] `--max-time` smaller than `--connect-timeout` bounds the connect too, as measured.
- [ ] A `Curl.Console.UnitTests` test shows a `dict://` transfer to a stalled dial ending with exit 28.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
