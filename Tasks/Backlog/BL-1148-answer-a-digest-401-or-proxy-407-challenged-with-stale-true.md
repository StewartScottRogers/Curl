---
id: BL-1148
title: Answer a Digest 401 or proxy 407 challenged with stale=true in the HTTP handler
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1148 — Answer a Digest 401 or proxy 407 challenged with stale=true in the HTTP handler

## Goal

When a request that sent a Digest answer gets a `401` (or a forward proxy's `407`) whose Digest challenge carries `stale=true`, `HttpProtocolHandler` sends a fresh Digest answer for the new nonce, as curl 8.21.0 does, instead of taking the response as the result.

## Context

BL-864 measured curl 8.21.0 (mingw, Schannel) re-answering a CONNECT's stale Digest `407` (ADR-0334): a fresh answer with a new cnonce and `nc=00000001`, no `Digest authentication problem, ignoring.` line, at most five reconnects when the proxy closes, then exit 7 `Could not connect to server`. `RankedHttpAuthenticator.ContinueAuthorizationAsync` (`Curl.Authentication.UnitLibrary`) returns `null` for a Digest value challenged again, and `grep -i stale Curl.Protocol.Http.UnitLibrary` finds nothing, so the HTTP handler gives up on a stale challenge today. `TcpConnector` uses `DigestStaleChallenge.IsOfferedIn` (internal to `Curl.Networking.UnitLibrary`); the HTTP handler needs its own check, or the authenticator could expose it (then touch `Curl.Authentication.UnitLibrary` too). Measure first with `Record-CurlExchange.ps1 -Connections 3`.

## Acceptance criteria

- [ ] Measured and pinned in Notes with the curl version: `curl -s -S -v --digest -u u:p http://127.0.0.1:<P>/` answered `401` Digest `nonce="a"`, then `401` Digest `nonce="b", stale=true`, then `200`; both with `Connection: close` and kept open; and a run of stale challenges, to learn curl's limit.
- [ ] An `HttpProtocolHandlerTests` case shows the requests match the measured ones and the transfer succeeds.
- [ ] A `401` without `stale=true` after a Digest answer is still taken as the result.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green and `Measure-CodeQuality.ps1` reports no failing member for the libraries touched.

## Notes

## Log

- 2026-10-01: Created.
