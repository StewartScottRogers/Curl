---
id: BL-5892
title: Fix fitness finding STD-211: NTLM through an NTLM proxy: when the origin handshake cannot complete, the port returns the 401 body with exit 0; curl keeps negotiating and fails with exit 47
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary/RankedHttpAuthenticator.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.NtlmThroughNtlmProxy.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5892 — Fix fitness finding STD-211: NTLM through an NTLM proxy: when the origin handshake cannot complete, the port returns the 401 body with exit 0; curl keeps negotiating and fails with exit 47

## Goal

STD-211 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: NTLM through an NTLM proxy: when the origin handshake cannot complete, the port returns the 401 body with exit 0; curl keeps negotiating and fails with exit 47. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Probable (2 runs Windows, 1 run Linux; the trigger is a proxy that opens a new upstream connection for every request) · **Area:** HTTP NTLM through an NTLM proxy. Both platforms.
- **Mock:** an NTLM proxy on 127.0.0.1:33013 that verifies NTLMv2 and forwards each request on a new upstream connection; an NTLM origin on 127.0.0.1:33000 `/ntlm` that keeps handshake state per connection (so the Type 3 never matches a Type 2 on the same connection).
- **Repro:** `curl -sS -w "%{http_code}\n" -x http://127.0.0.1:33013 -U user:pass --proxy-ntlm -u user:pass --ntlm http://127.0.0.1:33000/ntlm; echo $?`
- **Expected (REF 8.22.0 and REF2 8.18.0):** about 50 authentication rounds, then `curl: (47) Maximum (50) redirects followed`, `%{http_code}` 407, exit 47.
- **Actual (.NET, Windows and Linux):** after one failed Type 3 it prints the origin's 401 body (`type3 without type2 on this conn`), `%{http_code}` 401, exit 0.
- **Likely location:** the auth retry loop (`Curl.Authentication.UnitLibrary/RankedHttpAuthenticator.cs`) and the HTTP transfer logic that counts auth rounds against `--max-redirs`.
- **Acceptance check:** the repro exits 47 with `Maximum (50) redirects followed`.

## Acceptance criteria

- [x] the repro exits 47 with `Maximum (50) redirects followed`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: the HTTP handler, not `RankedHttpAuthenticator` (unchanged). The retry that answered
  a 407 re-sent the origin's NTLM Type 3; curl 8.22.0 sends it once. The proxy then forwarded a
  second, failed Type 3 to the origin, and nothing follows a Type 3, so the 401 ended the
  transfer. Without it the next bare `NTLM` starts the origin handshake again and the redirect
  limit ends the loop. Fix: `KeptAuthorizationForProxyRetry` in `HttpProtocolHandler.cs`.
  The resend after a dead connection is unchanged.
- `touches` widened to `HttpProtocolHandler.cs` and the new test file: no other task in Doing on
  `origin/work/dark-factory` named either (BL-5889 holds only the content decoder files).
- No ADR: no design choice, the requests are the ones measured from curl.
- Measured 2026-11-24 with `Record-CurlExchange.ps1` standing in for proxy and origin on one
  connection (the cycle is written out in BL-5925). Real curl 8.22.0 and this build both: exit
  47, `curl: (47) Maximum (50) redirects followed`, `%{http_code}` 407, `%{num_redirects}` 50,
  and the same requests once the NTLM tokens are cut to their message type.
- A proxy that instead forwards a request with no `Proxy-Authorization` gets the origin's 401
  from curl too (bare `NTLM`: "NTLM handshake rejected", exit 0), so the finding's mock must
  answer such a request with 407.
- `-v` still differs in `NTLM auth restarted` and some `auth using` lines: filed as BL-5925.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary` reported
  0 failing members. Fast tests: exit 0 across all test projects, `dotnet build` 0 warnings.
- Platform: the test drives scripted bytes and scripted tokens, so it is the same on Linux and macOS.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 9e142c5d 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. NTLM through an NTLM proxy with an origin handshake that cannot complete now exits 47 with Maximum (50) redirects followed, as curl does
