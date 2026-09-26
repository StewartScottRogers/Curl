---
id: BL-212
title: Tunnel through an HTTP proxy with CONNECT in the connector
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-162]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-212 — Tunnel through an HTTP proxy with CONNECT in the connector

## Goal

When `ConnectTarget.Proxy` is an HTTP proxy with tunnelling, the connector sends CONNECT, checks the answer, then runs TLS over the tunnel.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: `-p -x` answered 407 gives exit 7 `curl: (7) CONNECT tunnel failed, response 407`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] CONNECT request bytes are byte-equal to curl 8.21.0 (measured), including Proxy-Authorization.
- [ ] A non-2xx answer returns `CurlExitCode.CouldntConnect` (7) `CONNECT tunnel failed, response N`; an unresolvable proxy returns `CouldntResolveProxy` (5) with the measured message.
- [ ] TLS runs over the tunnel for an https target.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Plan item: N2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
