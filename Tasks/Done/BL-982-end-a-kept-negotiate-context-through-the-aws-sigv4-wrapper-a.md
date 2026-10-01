---
id: BL-982
title: End a kept Negotiate context through the aws-sigv4 wrapper and on a WebSocket upgrade
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-950]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-982 — End a kept Negotiate context through the aws-sigv4 wrapper and on a WebSocket upgrade

## Goal

A Negotiate context kept for its next leg is disposed of once the response to its token is not a challenge in the running `curl` binary and on `ws://`/`wss://`, as ADR-0248 does for the HTTP handler's authenticator.

## Context

- ADR-0248 (BL-950) added `IHttpAuthenticator.EndAuthorization(string sentAuthorization)` (default: nothing) and `HttpProtocolHandler` calls it for a non-401 (`Authorization`) or non-407 (`Proxy-Authorization`) response; `RankedHttpAuthenticator` hands it to `NegotiateHttpAuthenticator`, which disposes of the kept context unstepped.
- `Curl.Console/AwsSigV4HttpAuthenticator.cs` wraps the ranked authenticator in `CurlComposition` and does not override `EndAuthorization`, so in the composed binary the call reaches the default no-op and the context still lives until the process ends.
- `WsProtocolHandler` sends the pre-emptive Negotiate token through `CreateAuthorizationAsync` (ADR-0227) and never calls `EndAuthorization` after the 101 (or any other status, which is exit 22).
- curl 8.21.0 never steps a Negotiate token outside a 401/407 (lib/http.c line 3635), so nothing observable changes: no output, exit code or `-v` line.

## Acceptance criteria

- [x] `AwsSigV4HttpAuthenticator.EndAuthorization` forwards to the wrapped authenticator; a `Curl.Console.UnitTests` test pins it.
- [x] `WsProtocolHandler` calls `EndAuthorization` with the `Authorization` value it sent once the upgrade's response arrives; a `Curl.Protocol.Ws.UnitTests` test pins it with a recording authenticator.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Plan (small, done in-session): `AwsSigV4HttpAuthenticator.EndAuthorization` forwards to the wrapped other schemes unconditionally (a signed value keeps no context, so forwarding it is harmless and keeps the wrapper branch-free). `WsProtocolHandler` ends the sent `Authorization` value right after the upgrade's response head is read, whatever its status.
- Decision: unlike `HttpProtocolHandler`, the WebSocket handler ends the handshake on a 401 too, because curl sends the upgrade only once (ADR-0228) and with a token sent no 401 is stepped (`StepNegotiateForChallengeAsync` steps only when nothing was sent), so no next leg can ever use the kept context. Follows ADR-0248; no new ADR needed.
- Nothing observable changes (curl never steps Negotiate outside a 401/407), so no conformance measurement was needed.
- Tests: `AwsSigV4HttpAuthenticatorTests.EndAuthorization_KeptNegotiateValue_GoesToTheOtherSchemes`; `WsProtocolHandlerNegotiateTests.ExecuteAsync_NegotiateTokenSent_EndsItsHandshakeOnceTheResponseArrives` (101, 401, 403) and `ExecuteAsync_NothingSent_EndsNoHandshake`.
- Measure-CodeQuality: Curl.Console 100/100, 0 failing; Curl.Protocol.Ws.UnitLibrary 100/100, 0 failing.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. A kept Negotiate context is now ended through the aws-sigv4 wrapper and after a WebSocket upgrade's response
