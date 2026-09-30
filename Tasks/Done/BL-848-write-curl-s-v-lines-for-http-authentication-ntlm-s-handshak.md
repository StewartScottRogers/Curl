---
id: BL-848
title: Write curl's -v lines for HTTP authentication, NTLM's handshake lines included
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-526]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-848 — Write curl's -v lines for HTTP authentication, NTLM's handshake lines included

## Goal

`curl -v` against a server that asks for HTTP authentication writes the same `*` info
lines, in the same places, as the platform's curl: the "Server auth using <scheme> with
user '<user>'" line before each request carrying credentials, and NTLM's handshake
rejected / failure / problem lines.

## Context

BL-526 (ADR-0181,
`Documentation/Planning/Decisions/ADR-0181-http-ntlm-answers-in-three-legs-from-a-fresh-context-per-leg.md`)
made `--ntlm` answer in three legs through `NtlmHttpAuthenticator` and
`IHttpAuthenticator.ContinueAuthorizationAsync`, but chose not to write any `-v` line for
HTTP authentication. This task writes them.

What curl 8.21.0 (Windows, Schannel/SSPI) writes, from BL-526's Notes (section "Measured
2026-09-28" in `Tasks/Done/.../BL-526-answer-ntlm-challenges-for-http-as-the-platform-s-curl-does.md`
once BL-526 is done):

- `* Server auth using NTLM with user 'u'` before each request that carries an NTLM
  `Authorization` header. The Basic, Digest and Negotiate forms are presumed to be
  `* Server auth using Basic with user 'u'` and so on, but are not yet measured.
- A bare `NTLM` answering Type 3: `* NTLM handshake rejected`, then
  `* NTLM authentication problem, ignoring.`
- A second bare `NTLM` after Type 1: `* NTLM handshake failure (internal error)`.
- An unreadable Type 2 (curl's own NTLM, Linux, curl 8.18.0 OpenSSL):
  `* NTLM handshake failure (bad type-2 message)`. On Windows the same case fails with
  exit 94 (already done by BL-526), so no line is needed there.
- A Type 2 that is not base64: `* NTLM authentication problem, ignoring.`

Upstream source for the texts: `lib/http.c` (`Curl_output_auth`, "Server auth using %s
with user '%s'") and `lib/http_ntlm.c` (`Curl_input_ntlm`), curl 8.21.0.

Where to start: `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` writes `-v` info
lines through `ITransferEvents.ReportInfo` (see `ReportProtocolChosen`,
`ReportRequestSent`), with the texts kept in `HttpConnectionInfoLines.cs`. The NTLM
outcomes are decided in `Curl.Authentication.UnitLibrary/NtlmHttpAuthenticator.cs` and
`HandBuiltNtlmSecurityContext.cs`, so the authenticator has to tell the handler which
line applies. Prefer a seam inside the touched projects; if the only clean seam is a
member on `IHttpAuthenticator` (in `Curl.Protocol.Abstractions.UnitLibrary`), add that
project and its `.UnitTests` twin to `touches` before editing it and note why here.

## Acceptance criteria

- [x] The Basic, Digest and Negotiate `Server auth using ...` lines are measured with
      `Record-CurlExchange.ps1` (`-v`, 401 then 200) against curl 8.21.0 on Windows, and
      the stderr lines and their position relative to `> ` request lines are recorded in
      Notes with the curl version.
- [x] Tests in `Curl.Protocol.Http.UnitTests` pin, for each measured case, the exact
      line text and its position in the `-v` sequence (before the request header lines
      it precedes): `Server auth using NTLM with user 'u'` on both NTLM legs, the
      measured Basic/Digest/Negotiate lines, `NTLM handshake rejected` followed by
      `NTLM authentication problem, ignoring.`, `NTLM handshake failure (internal error)`,
      `NTLM handshake failure (bad type-2 message)` and
      `NTLM authentication problem, ignoring.` for non-base64.
- [x] Tests in `Curl.Authentication.UnitTests` cover each outcome the authenticator
      reports to the handler.
- [x] The lines reach stderr through `ITransferEvents.ReportInfo` (the seam the handler
      already uses for `-v`), not a new writer.
- [x] No line is written without `-v` (an existing `-sS` test of the NTLM exchange still
      shows empty stderr).
- [x] `dotnet build -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch
      coverage for `Curl.Protocol.Http.UnitLibrary` and `Curl.Authentication.UnitLibrary`.

## Notes

- Measure first; pin only measured text. If a presumed line (Basic/Digest/Negotiate)
  turns out different or absent, pin what curl writes.
- Record-CurlExchange's `-Script` mode answers several requests on one connection; BL-526
  used it for the NTLM legs.

### Measured 2026-09-30

`Record-CurlExchange.ps1 -Script`, `-v --ntlm -u u:p`, curl 8.21.0 (x86_64-w64-mingw32,
Schannel, SSPI) on Windows 11 and, with `-Curl wsl.exe -ListenAddress 172.26.96.1`, curl
8.18.0 (OpenSSL, its own NTLM) on Ubuntu. Every line below sits between the 401's status
line and its `WWW-Authenticate` header, on both builds, unless noted:

- 401 Type 2, 401 bare `NTLM`: `* NTLM handshake rejected`, `* NTLM authentication problem, ignoring.`
- 401 bare `NTLM` twice: on the second, `* NTLM handshake failure (internal error)`,
  `* NTLM authentication problem, ignoring.` (the second line was missing from BL-526's Notes).
- 401 `NTLM @@@notbase64`: `* NTLM authentication problem, ignoring.`
- 401 `NTLM TlRMTVNTUAACAAAA`, Ubuntu: `* NTLM handshake failure (bad type-2 message)`,
  `* NTLM authentication problem, ignoring.`, exit 0.
- 401 `NTLM TlRMTVNTUAACAAAA`, Windows: *not* no line, as the task presumed. The 401 body is
  ignored, then `* Connection #0 ... left intact`, `* Issue another request ...`,
  `* Reusing existing http: connection ...`, `* NTLM handshake failure (type-3 message):
  Status=0x80090308` followed by an empty line, `* Connection #0 ... left intact`,
  `curl: (94) ...`. No request is sent and no `Server auth using` line is written.
- Type 2 then 200: `* Server auth using NTLM with user 'u'` before both requests, no other line.

The Basic, Digest and Negotiate `Server auth using ...` lines and their places were already
measured against curl 8.21.0 on Windows and pinned by BL-954 (its Notes, 2026-09-29) and
BL-843; `HttpAuthUsingLines` writes them just before each `> ` request head, and
`HttpProtocolHandlerTests.AuthUsingVerbose`/`AuthRetryVerbose` pin them, NTLM's on both legs
included. This task did not re-measure them.

### What was built (ADR-0275)

- `Curl.Authentication.UnitLibrary`: `NtlmHandshakeLines` holds the texts;
  `NtlmHttpAuthenticator` reports them to `HttpAuthRequest.Events`, the seam Negotiate uses,
  so no member was added to `IHttpAuthenticator` and Abstractions was not touched.
- `Curl.Protocol.Http.UnitLibrary`: `HttpNtlmInfoLines.IsNtlmChallenge` defers a 401's
  headers from its NTLM `WWW-Authenticate` (and a 407's `Proxy-Authenticate` for a proxy's
  request, by the same curl code, unmeasured), so the lines land before it. A refusal to
  answer when Negotiate was not picked now becomes a retry plan that fails before sending
  (`WithAuthorizationFailure`), matching curl's placement of the Type 3 line and exit 94.
- Choices with a default taken: SSPI statuses other than the measured `SEC_E_INVALID_TOKEN`
  use `NegotiateFailureLines`' codes; any failure of curl's own NTLM to answer a Type 2
  other than the too-large refusal reports the bad type-2 lines.

### Touches added

`Documentation/Planning/Decisions` for ADR-0275 and its README row. No task in Doing
(BL-615, BL-974) names it.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. curl -v writes NTLM's handshake rejected/failure/problem lines before the challenge header and SSPI's type-3 failure line before exit 94, as curl 8.21.0 and 8.18.0 measured
