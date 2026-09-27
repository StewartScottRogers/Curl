---
id: BL-213
title: Tunnel through SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxies
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-162]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-213 — Tunnel through SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxies

## Goal

The connector performs SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h handshakes for a SOCKS `ConnectTarget.Proxy`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- RFC 1928/1929 and the SOCKS4/4a specifications.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Handshake bytes are scripted per version and match curl 8.21.0 (measured).
- [ ] Failures return `CurlExitCode.Proxy` (97) with the measured messages.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Plan item: N3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
