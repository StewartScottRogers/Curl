---
id: BL-981
title: Fail --negotiate -f with the context's failure as curl's error message
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-843]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-981 — Fail --negotiate -f with the context's failure as curl's error message

## Goal

`curl --negotiate -u : -f http://...` against a `401 Negotiate` with no ticket ends `curl: (22) <the context's failure line>` as curl 8.21.0 does, not `The requested URL returned error: 401`.

## Context

- Measured 2026-09-29 (BL-955 Notes) with curl 8.21.0 Schannel and `Record-CurlExchange.ps1`: `--negotiate -u : -sSf http://127.0.0.1:<port>/` against `401` + `WWW-Authenticate: Negotiate` writes `curl: (22) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`. curl's first `failf` fills the error buffer, and the Negotiate context's failure comes before the `-f` one.
- ADR-0231 (and its BL-955 amendment for ws://) says where the lines come from: `NegotiateHttpAuthenticator` reports them to `HttpAuthRequest.Events`; the HTTP handler records the first request's lines in `HttpInfoLineRecorder`. The WebSocket handler does the same for its failures (`WsProtocolHandler.WithFirstAuthFailure`).
- Check whether other failures after a failed Negotiate step (a timeout, exit 52) carry the line too, and measure the GSS-API wording's case on Linux if a Linux curl is at hand.

## Acceptance criteria

- [ ] A `Curl.Protocol.Http.UnitTests` test pins `TransferResult.ErrorMessage` as the Negotiate failure line for `--negotiate -u : -f` against a `401 Negotiate` with no ticket, one `OSCondition` test per platform wording.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
