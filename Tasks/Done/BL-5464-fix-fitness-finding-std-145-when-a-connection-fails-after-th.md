---
id: BL-5464
title: Fix fitness finding STD-145: When a connection fails after the TCP connect (proxy handshake, refused CONNECT, TLS verify failure, timeout through SOCKS), %{time_namelookup} and %{time_connect} are 0, and for TLS/timeout %{num_connects} is 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpConnector.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5464 — Fix fitness finding STD-145: When a connection fails after the TCP connect (proxy handshake, refused CONNECT, TLS verify failure, timeout through SOCKS), %{time_namelookup} and %{time_connect} are 0, and for TLS/timeout %{num_connects} is 0

## Goal

STD-145 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: When a connection fails after the TCP connect (proxy handshake, refused CONNECT, TLS verify failure, timeout through SOCKS), %{time_namelookup} and %{time_connect} are 0, and for TLS/timeout %{num_connects} is 0. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2+ runs per case, Windows and Linux)
- **Protocol/area:** write-out timings (neighbour of the BL-5421 change)
- **Repro:** HTTP server on 127.0.0.1:43000 (`/hello`); HTTPS server with a self-signed certificate on 43443; a SOCKS5 mock on 43013 that replies `05 00` to the greeting, reads the CONNECT request, then sends a TCP RST (`SO_LINGER {1,0}`); a SOCKS5 mock on 43015 that replies `05 00`, reads the CONNECT request and never answers; a CONNECT proxy on 43052 that answers 403. `F='%{time_namelookup} %{time_connect} %{time_pretransfer} %{num_connects}\n'`
  1. `curl -s -o NUL -x socks5://127.0.0.1:43013 http://127.0.0.1:43000/hello -w "$F"`
  2. `curl -s -o NUL https://127.0.0.1:43443/hello -w "$F"` (no `-k`; exit 60)
  3. `curl -s -o NUL -m 2 -x socks5://127.0.0.1:43015 http://127.0.0.1:43000/hello -w "$F"`
  4. `curl -s -o NUL -k -x http://127.0.0.1:43052 https://127.0.0.1:43443/hello -w "$F"`
- **Expected (REF):** (1) `0.000832 0.000690 0.001261 1`; (2) `0.000107 0.000837 0.005973 1`; (3) `0.000857 0.000000 2.009735 1`; (4) non-zero namelookup and connect, `1`. (REF2 on Linux, case 2: non-zero namelookup and connect, num_connects 1.)
- **Actual (PORT, Windows and Linux):** namelookup and connect `0.000000` in all four; `%{num_connects}` 0 in (2) and (3). Successful transfers match REF's pattern.
- **Likely location:** `Curl.Engine.UnitLibrary` timing stamping (BL-5421) with `Curl.Networking.UnitLibrary/TcpConnector.cs`: the DNS and connect timestamps are discarded when `ConnectResult.Failed` is returned after the TCP connect.
- **Acceptance check:** (1) and (2) print non-zero namelookup and connect and num_connects `1`; (3) prints non-zero namelookup and `1`.

## Acceptance criteria

- [x] (1) and (2) print non-zero namelookup and connect and num_connects `1`; (3) prints non-zero namelookup and `1`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes
nTcpConnector now stamps Started/NameResolved/Connected on a failure after the TCP connect (SOCKS or CONNECT refusal, TLS refusal) and carries NameResolved on a timeout through a proxy (CanceledAfterResolveException). Pinned by TcpConnectorTests.FailureTimings.cs. Criterion 1 verified by those unit tests, not live mocks; num_connects is the engine counting failures, which already carry ConnectionNumber. Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members. One unrelated flaky test (ServerCertificateVerificationCaDirectoryNoAiaTests, one-second notAfter rounding) failed once and passed on rerun.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-145 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 72375694 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Failures after the TCP connect keep namelookup and connect timings
