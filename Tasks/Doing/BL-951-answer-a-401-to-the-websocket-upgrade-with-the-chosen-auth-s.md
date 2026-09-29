---
id: BL-951
title: Answer a 401 to the WebSocket upgrade with the chosen auth scheme
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-842]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-951 — Answer a 401 to the WebSocket upgrade with the chosen auth scheme

## Goal

A `ws://` or `wss://` upgrade answered with `401` and a `WWW-Authenticate` challenge is sent again with the answer the authenticator gives - Digest, NTLM's legs, Negotiate's continuation, `--anyauth` - as curl 8.21.0 does, instead of failing at once with exit 22.

## Context

- `WsProtocolHandler` sends the upgrade with the pre-emptive `Authorization` from `IHttpAuthenticator.CreateAuthorizationAsync` (ADR-0227, BL-842) and fails any status but 101 with `Refused WebSocket upgrade: <code>` (ADR-0128).
- curl drives the upgrade through its HTTP code, so a 401 is answered as HTTP answers it: `CreateAuthorizationAsync` with the challenges when the request sent nothing, `ContinueAuthorizationAsync` when it sent a value (ADR-0181, ADR-0227), on the same connection when it stays open.
- Measure curl 8.21.0 with `Record-CurlExchange.ps1 -Script` (401 Digest then 101; 401 NTLM Type 2 then 101; 401 with no answerable challenge) before pinning bytes and exit codes.

## Acceptance criteria

- [ ] A `Curl.Protocol.Ws.UnitTests` test answers a 401 Digest challenge and pins both upgrade requests and the 101 that follows.
- [ ] A `Curl.Protocol.Ws.UnitTests` test pins a two-leg handshake through `ContinueAuthorizationAsync`.
- [ ] A 401 the authenticator does not answer still fails with exit 22 and the measured message.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
