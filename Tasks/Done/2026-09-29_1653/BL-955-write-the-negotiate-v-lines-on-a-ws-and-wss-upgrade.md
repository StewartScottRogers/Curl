---
id: BL-955
title: Write the Negotiate -v lines on a ws:// and wss:// upgrade
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-843]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Documentation/Planning/Decisions/ADR-0231-negotiate-failure-lines-are-worded-by-the-authenticator-and-placed-by-the-http-handler.md]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-955 — Write the Negotiate -v lines on a ws:// and wss:// upgrade

## Goal

Under `-v`, `--negotiate` on a `ws://` or `wss://` URL writes the context-failure line and `Server auth using Negotiate with user '<user>'` before the upgrade request, as curl 8.21.0 does and as BL-843 made the HTTP handler do.

## Context

- ADR-0231: `NegotiateHttpAuthenticator` reports a failed step to `HttpAuthRequest.Events`; only the HTTP handler sets it. `WsProtocolHandler` calls `CreateAuthorizationAsync` (around `WsProtocolHandler.cs:132`) with the default `NoTransferEvents`, so nothing is written.
- Measure `curl --negotiate -u : -v ws://127.0.0.1:<port>/` against a `401 Negotiate` with `Record-CurlExchange.ps1` first, to pin where the lines land relative to the upgrade request and the 401.

## Acceptance criteria

- [x] A `Curl.Protocol.Ws.UnitTests` test pins the measured lines, in curl's order, for `--negotiate -u : -v` on `ws://` with no ticket, one `OSCondition` test per platform wording.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-29 with curl 8.21.0 Schannel and `Record-CurlExchange.ps1`, `--negotiate -u : -v`
  and `--negotiate -v` on `ws://127.0.0.1:<port>/` against `401` + `WWW-Authenticate: Negotiate`:
  `using HTTP/1.x`, the SSPI failure, `Server auth using Negotiate with user ''`, the upgrade
  request, `Request completely sent off`, `< HTTP/1.1 401`, the failure again, `< WWW-Authenticate:
  Negotiate`, `< Content-Length: 4`, `Refused WebSocket upgrade: 401`, `< `, `closing connection #0`.
  Same lines with no `-u`. Also measured: the final message is `curl: (22) <failure line>`, with
  `-v` or `-sS`, and a `101` that closes with no frames ends `curl: (52) <failure line>`.
- Design (ADR-0231 amendment): `WsInfoLineRecorder` collects the authenticator's lines;
  `WsNegotiateInfoLines` mirrors the HTTP handler's Negotiate rules (Ws may not reference Http).
  A 401 offering Negotiate to an upgrade that sent no value is stepped through
  `CreateAuthorizationAsync` with the head's challenges, the value discarded (ADR-0228 still:
  one upgrade). A failed transfer's message is the first recorded line.
- Unmeasured choice: an upgrade that did send a Negotiate token is not stepped for the 401 (no
  ticket on hand to measure it).
- `touches` widened with ADR-0231's file, whose "the WebSocket handler does not yet (BL-955)"
  became false; no task in Doing names it (BL-943 touches ADR-0172 only).
- Follow-up filed: BL-981, the HTTP handler's `-f` exit 22 message after a Negotiate failure
  (measured: curl writes the failure line there too).
- Tests: `WsProtocolHandlerNegotiateTests` (per-platform `OSCondition` transcripts, error message,
  header placement, no-step cases), `WsNegotiateInfoLinesTests`, `WsInfoLineRecorderTests`.
  Ws: 227 passed, 1 skipped (the other platform). Measure-CodeQuality: Ws 100% line and branch,
  0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --negotiate -v on ws:// and wss:// writes curl's context-failure and Server auth lines, and fails with the failure as the message
