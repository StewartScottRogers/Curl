---
id: BL-842
title: Answer Negotiate continuation tokens in a 401 and over ws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-527]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-842 — Answer Negotiate continuation tokens in a 401 and over ws

## Goal

A Negotiate exchange of more than one leg works as curl 8.21.0's does: a `401` whose `WWW-Authenticate: Negotiate <token>` continues the context gets the context's next token on the same connection, and `ws://`/`wss://` answer Negotiate as HTTP does.

## Context

- BL-527 answers one leg: `NegotiateHttpAuthenticator` makes a fresh context per request and the HTTP handler retries once, only when the request that drew the 401 sent no `Authorization` (ADR-0173). SSPI can need several legs (NTLM inside SPNEGO on a domain without Kerberos, which ADR-0173 otherwise suppresses only for the first token), and a Kerberos acceptor may answer with a token.
- The context must live across the legs of one connection; `ISecurityContext.NextTokenAsync` already takes the incoming token. `IHttpAuthenticator` keeps no state today, so the task decides where the context lives (ADR needed if the contract changes). NTLM (BL-526) needs the same, so check whether it landed first and share the mechanism.
- `WsProtocolHandler` calls the synchronous `CreateAuthorization`; switch it to `CreateAuthorizationAsync`.
- Measure against a server that answers `401 Negotiate <token>` with `Record-CurlExchange.ps1` before pinning bytes.

## Acceptance criteria

- [ ] A `Curl.Protocol.Http.UnitTests` test with a scripted token source runs a two-leg Negotiate exchange and pins both requests.
- [ ] A `Curl.Protocol.Ws.UnitTests` test shows `--negotiate` reaching the WebSocket upgrade request.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
