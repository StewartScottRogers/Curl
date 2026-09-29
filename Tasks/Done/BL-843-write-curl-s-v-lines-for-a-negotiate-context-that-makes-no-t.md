---
id: BL-843
title: Write curl's -v lines for a Negotiate context that makes no token
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-527]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-843 — Write curl's -v lines for a Negotiate context that makes no token

## Goal

Under `-v`, `--negotiate` writes the lines curl 8.21.0 writes: the platform's context-failure line before the first request and again after the 401, and `Server auth using Negotiate with user '<user>'` before the request.

## Context

- ADR-0176 records the measured lines (BL-527 Notes). Windows (SSPI): `* InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package`. Linux (MIT GSS-API, curl 8.18.0): `* gss_init_sec_context() failed: No credentials were supplied, or the credentials were unavailable or inaccessible. SPNEGO cannot find mechanisms to negotiate. ` (trailing space). Both then `* Server auth using Negotiate with user ''`.
- The failure line comes from the `ISecurityContext` step's status; the seam (`SecurityContextStep`) carries no text yet, so the task decides where the text is made. `Server auth using Basic with user '...'` is not written for any scheme yet either; check curl for Basic and Digest and write them in the same change if the pattern is shared.
- Events go through `ITransferEvents.ReportInfo`.

## Acceptance criteria

- [x] A `Curl.Protocol.Http.UnitTests` or `Curl.Authentication.UnitTests` test pins each platform's lines, in curl's order, for `--negotiate -u : -v` against a `401 Negotiate` with no ticket, in its own `OSCondition` test per platform.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-09-29 with curl 8.21.0 Schannel and `Record-CurlExchange.ps1`: the Windows
  lines in ADR-0231. The 401's failure line lands between the status line and the
  `WWW-Authenticate: Negotiate` header - after an earlier `WWW-Authenticate: Basic` one when
  both are sent. Without `-u` curl still steps a context for the 401 and writes the failure
  (ADR-0227's "no 401 answered without a user" still holds: nothing is sent).
  `-u D\u:p` writes `with user 'D\u'`.
- Basic, Digest and Bearer share the `Server auth using <Scheme> with user '...'` line
  (measured); they are filed as BL-954 rather than widened into this task, since they change
  `-v` output across the HTTP tests and need more conditions measured. ws:// is BL-955.
- Design (ADR-0231): `NegotiateFailureLines` words the failure per platform in
  `Curl.Authentication.UnitLibrary`; `HttpAuthRequest.Events` carries the sink; the HTTP handler
  records the first request's lines (`HttpInfoLineRecorder`) and writes them after
  `using HTTP/1.x`, then `Server auth using Negotiate` (`HttpNegotiateInfoLines`); for a 401,
  `HttpResponseHeadReader.DefersFrom` holds the Negotiate challenge header and those after it
  until the retry is decided. `NegotiateHttpAuthenticator.ContinueAuthorizationAsync` now takes
  the request, for its events; `StepWithoutAnsweringAsync` is the no-`-u` step.
- `touches` widened: `Curl.Protocol.Abstractions.UnitLibrary` for `HttpAuthRequest.Events` (the
  seam had no way to carry the lines) and `Documentation/Planning/Decisions` for ADR-0231 and the
  ADR-0176 consequence. No task in Doing names either (checked 2026-09-29).
- Unmeasured choices, recorded in ADR-0231: the wording for `NoMechanism`, `Refused` and
  `MalformedToken`, and macOS using the GSS-API wording.
- Tests: `HttpProtocolHandlerTests.NegotiateVerbose.cs` pins the full event order per platform
  (`OSCondition`), `NegotiateFailureLinesTests` both wordings. Measure-CodeQuality: Authentication,
  Http and Abstractions at 100% line and branch, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --negotiate -v writes each platform curl's context-failure line and Server auth using Negotiate in curl's order
