---
id: BL-955
title: Write the Negotiate -v lines on a ws:// and wss:// upgrade
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-843]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-955 — Write the Negotiate -v lines on a ws:// and wss:// upgrade

## Goal

Under `-v`, `--negotiate` on a `ws://` or `wss://` URL writes the context-failure line and `Server auth using Negotiate with user '<user>'` before the upgrade request, as curl 8.21.0 does and as BL-843 made the HTTP handler do.

## Context

- ADR-0231: `NegotiateHttpAuthenticator` reports a failed step to `HttpAuthRequest.Events`; only the HTTP handler sets it. `WsProtocolHandler` calls `CreateAuthorizationAsync` (around `WsProtocolHandler.cs:132`) with the default `NoTransferEvents`, so nothing is written.
- Measure `curl --negotiate -u : -v ws://127.0.0.1:<port>/` against a `401 Negotiate` with `Record-CurlExchange.ps1` first, to pin where the lines land relative to the upgrade request and the 401.

## Acceptance criteria

- [ ] A `Curl.Protocol.Ws.UnitTests` test pins the measured lines, in curl's order, for `--negotiate -u : -v` on `ws://` with no ticket, one `OSCondition` test per platform wording.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
