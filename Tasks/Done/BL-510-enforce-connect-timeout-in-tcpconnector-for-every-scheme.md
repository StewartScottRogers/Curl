---
id: BL-510
title: Enforce --connect-timeout in TcpConnector for every scheme
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-498]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0117-the-connector-owns-the-connect-timeout-and-the-runner-owns-max-time.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-510 — Enforce --connect-timeout in TcpConnector for every scheme

## Goal

A TCP connect (and the TLS handshake, and a proxy tunnel, as curl counts them) that outlasts `--connect-timeout` (or the remaining `--max-time`) fails with exit 28 and curl 8.21.0's message for every scheme, as BL-498's ADR places it.

## Context

- Conformance audit 2026-09-28, row 12 (Blocker): `Curl.Networking.UnitLibrary/TcpConnector.cs` ignores `--connect-timeout`. The design is BL-498's ADR; read it first, including whether `ConnectTarget` (Abstractions) carries the deadline.
- Timings and failures: `ConnectResult`, `NumberedConnectFailure.cs`, `TlsFailureMessages.cs`; HTTP's own enforcement is ADR-0040.
- Inject `TimeProvider`; tests use a fake one and a dialer that never completes.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--connect-timeout 1` against a non-routable address (e.g. `10.255.255.1`) and against a loopback listener that accepts but never answers a TLS ClientHello (`https://`), with `-v`; stderr and exit code copied into Notes.
- [x] `Curl.Networking.UnitTests` tests on a fake `TimeProvider` pin exit 28 and the measured message for a stalled dial and a stalled handshake, and that a connect finishing in time is unaffected.
- [x] `--max-time` smaller than `--connect-timeout` bounds the connect too, as measured.
- [x] A `Curl.Console.UnitTests` test shows a `dict://` transfer to a stalled dial ending with exit 28.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

Measured 2026-09-28, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1`:

- `-v --connect-timeout 1 http://10.255.255.1/` (`-NoServer`): exit 28, stderr
  `*   Trying 10.255.255.1:80...` / `* Connection timed out after 1001 milliseconds` /
  `* closing connection #0` / `curl: (28) Connection timed out after 1001 milliseconds`.
- `-v --connect-timeout 5 -m 1 http://10.255.255.1/`: exit 28, same lines with `after 1000 milliseconds`.
- `-v --connect-timeout 1 dict://10.255.255.1/d:x`: exit 28, `Trying 10.255.255.1:2628...`, then
  `Connection timed out after 1010 milliseconds`, `closing connection #0`, `curl: (28) ...`.
- `-v -k --connect-timeout 1 https://127.0.0.1:18510/` against a listener that reads the ClientHello
  and answers nothing for 4 s (`-ResponseDelayMilliseconds 4000`): exit 28, `Trying`, the three
  `schannel:`/`ALPN:` lines, `* Connection timed out after 1006 milliseconds`, `closing connection #0`,
  `curl: (28) Connection timed out after 1006 milliseconds`.

What was built (ADR-0117 Decision 1, plus an amendment recorded in the ADR):

- `TcpConnector` takes `connectTimeout` (last constructor parameter; null or <= 0 is
  `DefaultConnectTimeout`, 300 s) and runs resolve, dials, tunnel and TLS under one
  `CancellationTokenSource(limit, timeProvider)` linked with the caller's token. An
  `OperationCanceledException` once the limit has passed on the clock becomes exit 28 with the
  measured message, also reported as a `-v` info line; an earlier cancellation escapes.
- `CurlComposition.ConnectTimeoutOf` passes the smaller of the connect timeout and a positive `-m`
  (decided by Claude under Stewart's delegation, amendment to ADR-0117): until BL-511's watchdog
  exists nothing else bounds a non-HTTP connect by `-m`, and for the first connect the two agree.
- `ConnectTarget` and `Curl.Protocol.Abstractions` are unchanged, as the ADR said.
- `touches` gained ADR-0117's file for the amendment; no task in Doing names it.
- Found: dict, gopher, telnet and mqtt build their `ConnectTarget` without `Events`, so their `-v`
  connect lines (the timeout line included) never print. Filed as BL-774; the console dict test
  pins exit 28 and the `curl: (28)` line only.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. TcpConnector ends any connect past --connect-timeout (or a smaller -m) with exit 28 and curl's Connection timed out message, for every scheme
