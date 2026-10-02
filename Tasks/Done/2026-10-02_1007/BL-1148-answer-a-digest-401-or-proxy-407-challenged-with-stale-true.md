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
completed: 2026-10-02
---
# BL-1148 — Answer a Digest 401 or proxy 407 challenged with stale=true in the HTTP handler

## Goal

When a request that sent a Digest answer gets a `401` (or a forward proxy's `407`) whose Digest challenge carries `stale=true`, `HttpProtocolHandler` sends a fresh Digest answer for the new nonce, as curl 8.21.0 does, instead of taking the response as the result.

## Context

BL-864 measured curl 8.21.0 (mingw, Schannel) re-answering a CONNECT's stale Digest `407` (ADR-0334): a fresh answer with a new cnonce and `nc=00000001`, no `Digest authentication problem, ignoring.` line, at most five reconnects when the proxy closes, then exit 7 `Could not connect to server`. `RankedHttpAuthenticator.ContinueAuthorizationAsync` (`Curl.Authentication.UnitLibrary`) returns `null` for a Digest value challenged again, and `grep -i stale Curl.Protocol.Http.UnitLibrary` finds nothing, so the HTTP handler gives up on a stale challenge today. `TcpConnector` uses `DigestStaleChallenge.IsOfferedIn` (internal to `Curl.Networking.UnitLibrary`); the HTTP handler needs its own check, or the authenticator could expose it (then touch `Curl.Authentication.UnitLibrary` too). Measure first with `Record-CurlExchange.ps1 -Connections 3`.

## Acceptance criteria

- [x] Measured and pinned in Notes with the curl version: `curl -s -S -v --digest -u u:p http://127.0.0.1:<P>/` answered `401` Digest `nonce="a"`, then `401` Digest `nonce="b", stale=true`, then `200`; both with `Connection: close` and kept open; and a run of stale challenges, to learn curl's limit.
- [x] An `HttpProtocolHandlerTests` case shows the requests match the measured ones and the transfer succeeds.
- [x] A `401` without `stale=true` after a Digest answer is still taken as the result.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green and `Measure-CodeQuality.ps1` reports no failing member for the libraries touched.

## Notes

- Measured curl 8.21.0 (x86_64-w64-mingw32, Schannel) 2026-10-02 with `Record-CurlExchange.ps1 -Port 18148`, `-s -S -v --digest -u u:p http://127.0.0.1:18148/`, each `401` being `HTTP/1.1 401 Unauthorized` + `WWW-Authenticate: Digest realm="r", nonce="<n>", qop="auth"[, stale=true]` + `Content-Length: 0` [+ `Connection: close`]:
  - `a`, `b` stale, `200`, all `Connection: close` (`-Connections 3`): three connections, requests with no `Authorization`; `Digest username="u",realm="r",nonce="a",uri="/",cnonce="370cf856b91684edfd74ca6d21b5bebb",nc=00000001,response="8e5f4ff511caf60a1c5d62bf94390e19",qop="auth"`; `Digest username="u",realm="r",nonce="b",uri="/",cnonce="fa452aa0c29c5f74b6287443cc695e23",nc=00000001,response="2c3e85ee0f1f96fd9dc24992aac4b9cc",qop="auth"` (each `GET / HTTP/1.1`, `Host: 127.0.0.1:18148`, `Authorization` if any, `User-Agent: curl/8.21.0`, `Accept: */*`). `-v`: `Issue another request to this URL` after each 401, no `Digest authentication problem, ignoring.` line; exit 0.
  - The same kept open (`-Connections 1 -HoldOpenMilliseconds 1500 -AnswerHeldRequests 2`): all three requests on connection #0 (`left intact` each time), nonce `a` cnonce `7c8d53b70ecd1db6c969ae2cad2a18d6` response `388598f0eda02bfd687cb83fa9fee4b0`, nonce `b` cnonce `69f374a2527dd5c0db29fba426e68bb3` response `beae51319e57b3584ac6ec337011a4ef`; exit 0.
  - A run of stale challenges (`a`, then `b`..`z` stale, `-Connections 30`): curl answered every one, 29 requests, then waited on a connect the recorder no longer accepted until killed after 30 minutes. So curl has no limit on stale `401` answers (the tunnel's five-reconnect cap, ADR-0334, is the CONNECT path's).
  - `a`, then `b` without stale (`-Connections 2`): two requests, `* Digest authentication problem, ignoring.` before the second `WWW-Authenticate`, the 401 is the result, exit 0. Curl does not write that line yet: filed as BL-1175.
- Decision (ADR-0361, Decided by Claude under Stewart's delegation): `HttpDigestStaleChallenge` in the HTTP library (a copy of `Curl.Networking`'s `DigestStaleChallenge`, as protocol libraries reference no other library), and `AnswerChallengesAsync` answers a sent `Digest ` value challenged stale through `CreateAuthorizationAsync`. The one path serves the origin `401` and a forward proxy's `407`. No cap, as measured.
- Tests use the real `RankedHttpAuthenticator` with the measured cnonces injected: the hashes match curl's; the `Authorization` layout is curl's own-code Digest format (ADR-0025: blanks after commas, `qop=auth` before `response`), as `TcpConnectorTests.ProxyDigestStale` pins it.
- Results: build clean; fast run green (Http 1660 passed, 4 skipped); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A Digest 401 or forward proxy 407 challenged with stale=true is answered afresh with the new nonce, as curl 8.21.0
