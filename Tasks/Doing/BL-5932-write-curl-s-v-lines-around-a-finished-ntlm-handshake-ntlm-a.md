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
completed:
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

- [ ] For the exchange above, the port's `*` lines about authentication (`auth using` and `NTLM`) are the ones curl 8.22.0 writes, in the same positions.
- [ ] A unit test in `Curl.Original.Protocol.Http.UnitTests` pins those lines.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: c4f7fe93 164d3fb1 3d836d5e.
