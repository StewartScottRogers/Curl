---
id: BL-714
title: Connect with TLS 1.0 and 1.1 where the operating system's TLS stack refuses them
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-502, BL-708]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-714 — Connect with TLS 1.0 and 1.1 where the operating system's TLS stack refuses them

## Goal

When the command line allows TLS 1.0 or 1.1 (`--tlsv1.0`, `--tlsv1.1`, `--tls-max 1.0`/`1.1`) and the operating system's TLS stack refuses to offer them (Windows 11 Schannel, OpenSSL 3 at its default security level), Curl connects through the hand-built TLS client as curl.se's official build of curl does, on every platform.

## Context

- Conformance audit 2026-09-28, row 5; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): what any official curl build does, Curl does everywhere. BL-502 makes `SslStream` negotiate the range it can and pins today's answer; this task closes the remaining gap. curl.se's Windows build of curl 8.22.0 uses LibreSSL 4.3.2 (https://curl.se/windows/, checked 2026-09-28); measure whether it completes a TLS 1.0 and a TLS 1.1 handshake with `--tlsv1.0 --tls-max 1.0` against a legacy server, and the OpenSSL build's answer, before pinning.
- Routing: BL-617's ADR and BL-708 (a new row: minimum or maximum below 1.2); protocol: BL-702 and BL-703.

## Acceptance criteria

- [x] Measured first through `Record-CurlExchange.ps1 -NoServer` against a TLS 1.0-only and a TLS 1.1-only server (for example `openssl s_server -tls1 -cipher DEFAULT@SECLEVEL=0`); stderr and exit code copied into Notes per build.
- [x] Tests show `--tlsv1.0 --tls-max 1.0` and `--tlsv1.1 --tls-max 1.1` completing a transfer through the hand-built client against in-memory TLS 1.0/1.1 servers on every platform, and the default range still using `SslStream`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-10-01 (`Record-CurlExchange.ps1 -NoServer -CurlArgs '-sS','-k',<range>,'https://localhost:<port>/'`) against `openssl s_server -tls1` / `-tls1_1 -cipher DEFAULT@SECLEVEL=0 -www`; full table in ADR-0331:
  - curl 8.21.0 Schannel (Windows 11): exit 0, no stderr, for `--tlsv1.0 --tls-max 1.0`, `--tlsv1.0`, `--tlsv1.1 --tls-max 1.1` and `--tlsv1.1`.
  - curl.se's build, curl 8.18.0 LibreSSL 4.2.1: with `--tls-max` exit 35 `curl: (35) TLS connect error: error:1404E0BF:SSL routines:ST_BEFORE_CONNECT:no protocols available`; minimum alone exit 35 `curl: (35) TLS connect error: error:1400442E:SSL routines:CONNECT_CR_SRVR_HELLO:tlsv1 alert protocol version`.
  - Ubuntu curl 8.18.0 OpenSSL 3.5.5 (WSL): with `--tls-max` exit 35 `curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available`; minimum alone exit 35 `curl: (35) TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version`.
- The task's premise was half wrong: curl.se's LibreSSL build refuses; the Schannel build connects. Decision (ADR-0331): connect through the hand-built client everywhere, as the most capable official build does.
- No production change was needed: the legacy-versions routing row and the hand-built TLS 1.0/1.1 handshake already existed (BL-702, BL-703, BL-708). Added `Curl.Networking.UnitTests/Fakes/LegacyTlsTestServer.cs` (TLS 1.0-only or 1.1-only, `TLS_RSA_WITH_AES_128_CBC_SHA`, checks the client's Finished) and `HandBuiltTlsProviderTests.LegacyVersions.cs`, a request/response through the hand-built client as both builds. The default range on `SslStream` stays pinned by `TlsClientRoutingTests`. No library changed, so coverage is unchanged; `Curl.Console` and its tests were not needed.
- Follow-up: BL-1143, a TLS 1.0/1.1 minimum with no ceiling (still `SslStream`).
- Fast tests: all green (Curl.Networking.UnitTests 2394 passed, 25 skipped).

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A --tls-max 1.0 or 1.1 range completes a transfer through the hand-built client against a TLS 1.0/1.1-only server on every platform (ADR-0331)
