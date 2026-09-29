---
id: BL-848
title: Write curl's -v lines for HTTP authentication, NTLM's handshake lines included
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-526]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
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

- [ ] The Basic, Digest and Negotiate `Server auth using ...` lines are measured with
      `Record-CurlExchange.ps1` (`-v`, 401 then 200) against curl 8.21.0 on Windows, and
      the stderr lines and their position relative to `> ` request lines are recorded in
      Notes with the curl version.
- [ ] Tests in `Curl.Protocol.Http.UnitTests` pin, for each measured case, the exact
      line text and its position in the `-v` sequence (before the request header lines
      it precedes): `Server auth using NTLM with user 'u'` on both NTLM legs, the
      measured Basic/Digest/Negotiate lines, `NTLM handshake rejected` followed by
      `NTLM authentication problem, ignoring.`, `NTLM handshake failure (internal error)`,
      `NTLM handshake failure (bad type-2 message)` and
      `NTLM authentication problem, ignoring.` for non-base64.
- [ ] Tests in `Curl.Authentication.UnitTests` cover each outcome the authenticator
      reports to the handler.
- [ ] The lines reach stderr through `ITransferEvents.ReportInfo` (the seam the handler
      already uses for `-v`), not a new writer.
- [ ] No line is written without `-v` (an existing `-sS` test of the NTLM exchange still
      shows empty stderr).
- [ ] `dotnet build -warnaserror` is clean and
      `dotnet test --filter "TestCategory!=Integration"` is green.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch
      coverage for `Curl.Protocol.Http.UnitLibrary` and `Curl.Authentication.UnitLibrary`.

## Notes

- Measure first; pin only measured text. If a presumed line (Basic/Digest/Negotiate)
  turns out different or absent, pin what curl writes.
- Record-CurlExchange's `-Script` mode answers several requests on one connection; BL-526
  used it for the NTLM legs.

## Log

- 2026-09-28: Created.
