---
id: BL-5945
title: Write curl's Server auth using NTLM line for a redirected request sent without a header on an NTLM-authenticated connection
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.NtlmNegotiation.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.AuthReports.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NtlmAuthenticatedConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.RequestPlan.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5945 — Write curl's Server auth using NTLM line for a redirected request sent without a header on an NTLM-authenticated connection

## Goal

With `--ntlm -L -v`, a redirected request that reuses the authenticated connection writes the
same `auth using` line as curl 8.22.0.

## Context

Found by BL-5932. `OnNtlmAuthenticatedConnection` in `HttpProtocolHandler.NtlmNegotiation.cs` drops the Type 1
from such a request (BL-3003), and the handler then writes no `* Server auth using NTLM with
user '...'` line. libcurl's `output_auth_headers` writes it whenever NTLM is picked, header or
not (measured for the retry case in BL-5932). Not measured for a redirect: measure real curl
with `Record-CurlExchange.ps1` first, including a bare `NTLM` 401 answering that request
(`NTLM auth restarted`).

## Acceptance criteria

- [x] For a measured `--ntlm -L -v` redirect on one connection, the port's `*` lines about authentication are curl 8.22.0's, in the same positions.
- [x] A unit test in `Curl.Original.Protocol.Http.UnitTests` pins those lines.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-25 with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds`,
  `-AnswerHeldRequests`; curl 8.22.0 Schannel, `--ntlm -u u:p -L -v --no-progress-meter`,
  401 Type 2, 302 to `/next`, then 200; the same with a bare `NTLM` 401 before the 200; the
  same as a `-d` POST). Use `--no-progress-meter`: without it the progress meter shares the
  line of the redirected request's `Server auth using` line, which hides it from a `^\*` filter.
- curl writes `* Server auth using NTLM with user 'u'` before the redirected request that sends
  no header, and `* NTLM auth restarted` before the bare challenge that answers it.
- Fix: `WithoutNtlmType1Probe` marks the plan `NtlmType3WentOut`, the state BL-5932 added, so
  both lines come from the existing code. After the fix the `*` and header lines of this build
  and curl differ in none of the three runs, apart from curl's `sending last upload chunk` and
  `Curl_xfer_send` lines of its debug build, which are not authentication lines.
- `HttpProtocolHandler.RequestPlan.cs` added to `touches`: the fix is there, and no other task
  in Doing names it.
- No ADR: no design choice, the lines are measured from curl.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary` reported
  0 failing members. `dotnet build` 0 warnings, fast tests exit 0.
- Platform: the test drives scripted bytes and values, the same on Linux and macOS.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: 8ba15c70 164d3fb1 3d836d5e.
- 2026-11-25: Doing -> Done. The redirected request writes curl's NTLM auth using and auth restarted lines
