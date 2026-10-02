---
id: BL-1175
title: Write Digest authentication problem, ignoring. for a refused Digest answer in the HTTP handler
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1175 — Write Digest authentication problem, ignoring. for a refused Digest answer in the HTTP handler

## Goal

Under `-v`, when a request that sent a Digest answer gets a `401` (or a forward proxy's `407`) whose Digest challenge does not carry `stale=true`, `HttpProtocolHandler` writes `* Digest authentication problem, ignoring.` just before the challenge header, as curl 8.21.0 does.

## Context

Measured in BL-1148 (curl 8.21.0, mingw, Schannel): `curl -s -S -v --digest -u u:p http://127.0.0.1:<P>/` answered a `401` Digest `nonce="a"`, then a `401` Digest `nonce="b"` (no stale), writes `* Digest authentication problem, ignoring.` between `< HTTP/1.1 401 Unauthorized` and `< WWW-Authenticate: Digest realm="r", nonce="b", qop="auth"`, and exits 0 with the 401. A stale challenge writes no such line. `HttpAuthProblemLines` writes the line only for Basic and Bearer today, and it splits challenges at commas, which would count each Digest parameter as a challenge; Digest needs its own reading (one line per Digest challenge, none when it is stale). Measure first with `Record-CurlExchange.ps1 -Connections 2`, including a header with two Digest challenges.

## Acceptance criteria

- [ ] Measured and pinned in Notes with the curl version, for one and for two Digest challenges in the refusing `401`, and for a proxy `407`.
- [ ] `HttpAuthProblemLinesTests` and an `HttpProtocolHandlerTests` case show the line written where curl writes it, and not for a stale challenge.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green and `Measure-CodeQuality.ps1` reports no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
