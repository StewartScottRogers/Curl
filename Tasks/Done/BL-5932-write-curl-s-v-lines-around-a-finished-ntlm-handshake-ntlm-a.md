---
id: BL-5932
title: Write curl's -v lines around a finished NTLM handshake: NTLM auth restarted, and the auth using lines as curl 8.22.0 writes them
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpNtlmInfoLines.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NtlmThroughNtlmProxy.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NtlmVerbose.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-25
---
# BL-5932 — Write curl's -v lines around a finished NTLM handshake: NTLM auth restarted, and the auth using lines as curl 8.22.0 writes them

## Goal

With `--proxy-ntlm` and `--ntlm` together, `-v` writes the same `*` lines as curl 8.22.0
while a handshake that never completes is restarted until the redirect limit.

## Context

Found by BL-5892, which made the requests and the exit code match; only `-v` lines differ.
Measured with `Record-CurlExchange.ps1` (`-Connections 1 -HoldOpenMilliseconds 2000
-AnswerHeldRequests 70`, curl 8.22.0 Schannel) for `curl -sS -v -x http://127.0.0.1:33013
-U user:pass --proxy-ntlm -u user:pass --ntlm http://127.0.0.1:33000/ntlm`, answering in a
repeating cycle of: 407 with a Type 2, 401 with a Type 2, 407 bare `NTLM`, 407 with a Type 2,
401 bare `NTLM`, 407 bare `NTLM`. The Type 2 must be one SSPI accepts: take it from a server
`System.Net.Security.NegotiateAuthentication` (package `NTLM`) given curl's Type 1.

Compared with curl's, the port's stderr lacks:

1. `* NTLM auth restarted`, which curl writes before the `< ...-Authenticate: NTLM` line of a
   bare challenge that answers a request sent after that side's Type 3 had gone out.
2. Some `* Proxy auth using NTLM with user 'user'` and `* Server auth using NTLM with user
   'user'` lines, which curl writes for a request even when that side sends no header.

The authenticator is stateless, so the handler has to remember that a side's Type 3 was sent
(`HttpProtocolHandler.cs`, `HttpNtlmInfoLines.cs`, `HttpAuthUsingLines.cs`).

## Acceptance criteria

- [x] For the exchange above, the port's `*` lines about authentication (`auth using` and `NTLM`) are the ones curl 8.22.0 writes, in the same positions.
- [x] A unit test in `Curl.Original.Protocol.Http.UnitTests` pins those lines.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Fix: the request plan carries, per side, "this request sends no header because the NTLM Type 3
  went out earlier" (libcurl's `NTLMSTATE_LAST`). Such a request still writes `<side> auth using
  NTLM`, and a bare `NTLM` challenge to it writes `NTLM auth restarted` before its header.
- Also: a challenge's deferred header lines are now released even when the redirect limit refuses
  its retry, so `NTLM auth restarted` and the challenge come before `Maximum (50) redirects
  followed`, as curl orders them.
- Measured 2026-11-25 with `Record-CurlExchange.ps1` (curl 8.22.0 Schannel, the Type 2 of the
  `ProxyNtlmType2` test constant, which SSPI accepts). Auth, NTLM, status and authentication
  header lines of curl and this build: 400 each, no difference; both exit 47.
- The lines after the last `< Content-Length: 0` still differ (not authentication lines): filed
  as BL-5941.
- No ADR: no design choice, the lines are measured from curl.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary` reported
  0 failing members. `dotnet build` 0 warnings, fast tests exit 0.
- Platform: the tests drive scripted bytes and scripted tokens, the same on Linux and macOS.

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: c4f7fe93 164d3fb1 3d836d5e.
- 2026-11-25: Doing -> Done. With --proxy-ntlm and --ntlm together, -v writes curl 8.22.0's auth using and NTLM auth restarted lines
