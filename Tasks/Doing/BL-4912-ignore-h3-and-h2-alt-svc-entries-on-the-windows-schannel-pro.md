---
id: BL-4912
title: Ignore h3 and h2 alt-svc entries on the Windows Schannel profile instead of switching protocol
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/QuicDialer.cs, Curl.Networking.UnitTests/AltSvcPlatformProtocolTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4912 — Ignore h3 and h2 alt-svc entries on the Windows Schannel profile instead of switching protocol

## Goal

On Windows, where neither HTTP/3 nor (by default) HTTP/2 is offered, an alt-svc file never moves a transfer off the origin; the origin is used as curl 8.22.0 Schannel does. Covers findings STD-009 and STD-013.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-009 (Medium, confirmed: reproduced at least twice).

- Repro: HTTPS server on TCP 18301 (body `ok`), nothing on UDP 18398. `printf "h1 localhost 18301 h3 localhost 18398 \"20301231 00:00:00\" 0 0\n" > alt.txt`; `curl -sS https://localhost:18301/ --alt-svc alt.txt --cacert ca.pem --ssl-no-revoke -w "%{http_version} %{remote_port}\n"`. STD-013: same with an `h2 localhost 18320` entry and an HTTP/2 server on 18320.
- Expected: REF 8.22.0: `ok`, `1.1 18301`, exit 0, for both the h3 and the h2 entry.
- Actual: h3: exit 56, `0 -1`, `curl: (56) QUIC: recvfrom() unexpectedly returned -1 (errno=10054; Connection was reset)`, no fallback; when an HTTP/3 server answers, TLS errors switch to OpenSSL wording. h2: served by the h2 server, `2 18320`, although `-V` lists no HTTP2 (ADR-0141).
- Where to start: Alt-svc candidate selection feeding `QuicDialer.cs`; apply the per-platform protocol availability.

## Acceptance criteria

- [ ] The h3 repro exits 0 and prints `ok` and `1.1 18301`.
- [ ] The h2 repro prints `ok` and `1.1 18301`.
- [ ] Linux and macOS behaviour (where the build offers h2/h3) is unchanged and pinned in its own test.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: f7c46fe8 00c24195 f9789258 a0b49a29.
