---
id: BL-981
title: End a kept Negotiate context through the aws-sigv4 wrapper and on a WebSocket upgrade
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-950]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-981 — End a kept Negotiate context through the aws-sigv4 wrapper and on a WebSocket upgrade

## Goal

A Negotiate context kept for its next leg is disposed of once the response to its token is not a challenge in the running `curl` binary and on `ws://`/`wss://`, as ADR-0248 does for the HTTP handler's authenticator.

## Context

- ADR-0248 (BL-950) added `IHttpAuthenticator.EndAuthorization(string sentAuthorization)` (default: nothing) and `HttpProtocolHandler` calls it for a non-401 (`Authorization`) or non-407 (`Proxy-Authorization`) response; `RankedHttpAuthenticator` hands it to `NegotiateHttpAuthenticator`, which disposes of the kept context unstepped.
- `Curl.Console/AwsSigV4HttpAuthenticator.cs` wraps the ranked authenticator in `CurlComposition` and does not override `EndAuthorization`, so in the composed binary the call reaches the default no-op and the context still lives until the process ends.
- `WsProtocolHandler` sends the pre-emptive Negotiate token through `CreateAuthorizationAsync` (ADR-0227) and never calls `EndAuthorization` after the 101 (or any other status, which is exit 22).
- curl 8.21.0 never steps a Negotiate token outside a 401/407 (lib/http.c line 3635), so nothing observable changes: no output, exit code or `-v` line.

## Acceptance criteria

- [ ] `AwsSigV4HttpAuthenticator.EndAuthorization` forwards to the wrapped authenticator; a `Curl.Console.UnitTests` test pins it.
- [ ] `WsProtocolHandler` calls `EndAuthorization` with the `Authorization` value it sent once the upgrade's response arrives; a `Curl.Protocol.Ws.UnitTests` test pins it with a recording authenticator.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
