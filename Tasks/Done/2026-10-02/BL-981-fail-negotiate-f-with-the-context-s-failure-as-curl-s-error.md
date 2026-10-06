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
completed: 2026-10-02
---
# BL-981 — Fail --negotiate -f with the context's failure as curl's error message

## Goal

`curl --negotiate -u : -f http://...` against a `401 Negotiate` with no ticket ends `curl: (22) <the context's failure line>` as curl 8.21.0 does, not `The requested URL returned error: 401`.

## Context

- Measured 2026-09-29 (BL-955 Notes) with curl 8.21.0 Schannel and `Record-CurlExchange.ps1`: `--negotiate -u : -sSf http://127.0.0.1:<port>/` against `401` + `WWW-Authenticate: Negotiate` writes `curl: (22) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`. curl's first `failf` fills the error buffer, and the Negotiate context's failure comes before the `-f` one.
- ADR-0231 (and its BL-955 amendment for ws://) says where the lines come from: `NegotiateHttpAuthenticator` reports them to `HttpAuthRequest.Events`; the HTTP handler records the first request's lines in `HttpInfoLineRecorder`. The WebSocket handler does the same for its failures (`WsProtocolHandler.WithFirstAuthFailure`).
- Check whether other failures after a failed Negotiate step (a timeout, exit 52) carry the line too, and measure the GSS-API wording's case on Linux if a Linux curl is at hand.

## Acceptance criteria

- [x] A `Curl.Protocol.Http.UnitTests` test pins `TransferResult.ErrorMessage` as the Negotiate failure line for `--negotiate -u : -f` against a `401 Negotiate` with no ticket, one `OSCondition` test per platform wording.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-10-02: `HttpProtocolHandler.ExecuteAsync` now gives any failed transfer (exit != 0) the first line the authenticator reported for the first request's `Authorization` value as `ErrorMessage`, as `WsProtocolHandler.WithFirstAuthFailure` does for ws:// (ADR-0344). Only Negotiate reports a line there, so no scheme check is needed.
- Later failures (exit 52 after an empty reply) carry the line too: curl's error buffer keeps the first `failf` whatever fails later; pinned by `ExecuteAsync_NegotiateWithoutATicketEmptyReply_FailsWithTheFailureLine`. Not re-measured against real curl in this run; based on BL-955's ws:// measurements of exit 22 and 52.
- GSS-API wording is the authenticator's existing per-platform text (NegotiateFailureLinesTests); no Linux curl was at hand to re-measure its case.
- Left as is: a context failure reported while answering a 401 under `--anyauth` does not name the error yet (ADR-0344, Consequences).
- Tests: `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NegotiateFail.cs`. Http library 100% line and branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --negotiate -f without a ticket fails exit 22 with the context's failure line as curl's error
