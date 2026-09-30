---
id: BL-842
title: Answer Negotiate continuation tokens in a 401 and over ws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-527]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-842 — Answer Negotiate continuation tokens in a 401 and over ws

## Goal

A Negotiate exchange of more than one leg works as curl 8.21.0's does: a `401` whose `WWW-Authenticate: Negotiate <token>` continues the context gets the context's next token on the same connection, and `ws://`/`wss://` answer Negotiate as HTTP does.

## Context

- BL-527 answers one leg: `NegotiateHttpAuthenticator` makes a fresh context per request and the HTTP handler retries once, only when the request that drew the 401 sent no `Authorization` (ADR-0176). SSPI can need several legs (NTLM inside SPNEGO on a domain without Kerberos, which ADR-0176 otherwise suppresses only for the first token), and a Kerberos acceptor may answer with a token.
- The context must live across the legs of one connection; `ISecurityContext.NextTokenAsync` already takes the incoming token. `IHttpAuthenticator` keeps no state today, so the task decides where the context lives (ADR needed if the contract changes). NTLM (BL-526) needs the same, so check whether it landed first and share the mechanism.
- `WsProtocolHandler` calls the synchronous `CreateAuthorization`; switch it to `CreateAuthorizationAsync`.
- Measure against a server that answers `401 Negotiate <token>` with `Record-CurlExchange.ps1` before pinning bytes.

## Acceptance criteria

- [x] A `Curl.Protocol.Http.UnitTests` test with a scripted token source runs a two-leg Negotiate exchange and pins both requests.
- [x] A `Curl.Protocol.Ws.UnitTests` test shows `--negotiate` reaching the WebSocket upgrade request.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Plan and decision: ADR-0227. NTLM (BL-526) had already landed with a stateless
  continuation (ADR-0181), replaying a fresh context; Negotiate cannot, since a Kerberos
  authenticator differs every time. So `NegotiateHttpAuthenticator` keeps a `ContinueNeeded`
  context in a concurrent dictionary keyed by the `Authorization` value it made, and
  `ContinueAuthorizationAsync` steps it with the 401's `Negotiate` token. The HTTP handler and
  the `IHttpAuthenticator` shape are unchanged; `RankedHttpAuthenticator` routes a continuation
  to Negotiate when the value sent starts `Negotiate `, for the origin, with `-u` given (libcurl
  answers no 401 without a user).
- `WsProtocolHandler` now calls `CreateAuthorizationAsync`. It still answers no 401 (exit 22);
  that is BL-951.
- Measured 2026-09-29, `Record-CurlExchange.ps1`, curl 8.21.0 SSPI, no domain,
  `--negotiate -u : -v` against `401 Negotiate oRQw...`: one request with no `Authorization`,
  `SEC_E_NO_CREDENTIALS` then `SEC_E_INVALID_HANDLE`, body, exit 0 - what Curl does. A working
  multi-leg exchange needs a KDC no lane has, so the two-leg bytes are pinned from curl's
  `Curl_input_negotiate` with scripted tokens.
- Touches: added `Curl.Protocol.Abstractions.UnitLibrary` (the `IHttpAuthenticator` remarks
  said no call keeps state, now false) and `Documentation/Planning/Decisions` (ADR-0227); no
  task in Doing names either.
- Quality: Authentication, Http, Ws and Abstractions libraries all 100% line and branch, 0
  failing members. Tests: Authentication 636, Http 1377, Ws 184 passed.
- Follow-ups filed: BL-950 (check the acceptor's final token in a 2xx, mutual auth), BL-951
  (answer a 401 to the WebSocket upgrade).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A 401 carrying a Negotiate token gets the same context's next token on the same connection, and ws:// sends Negotiate pre-emptively
