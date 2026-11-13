---
id: BL-5118
title: Report the 401 in -w variables when Digest or NTLM authentication fails with exit 94
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/AuthenticationFailureWriteOutTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5118 — Report the 401 in -w variables when Digest or NTLM authentication fails with exit 94

## Goal

When Digest or NTLM authentication fails with exit 94 after a 401 was received, `%{http_code}`, `%{response_code}` and `%{num_headers}` report that 401 response (`401 401 4`), as upstream curl does, instead of `000 000 0`.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-068 (Low, Confirmed in 6+ runs, Digest and NTLM). Area: HTTP `--digest` / `--ntlm`, `-w`.

**Repro.**
1. Run a server on 127.0.0.1:21004 that always answers `401 Unauthorized` with `WWW-Authenticate: Digest realm="r@x", nonce="dcd98b7102dd2f0e8b11d0f600bfb0c093", algorithm=SHA-256, qop="auth"` and `Content-Length: 5` (SHA-256 is unsupported by SSPI Digest on Windows).
2. `curl -sS -i --digest -u user:pass http://127.0.0.1:21004/d/x -w "[%{http_code} %{response_code} %{num_headers}]"`
3. NTLM variant: a server answering `401` with `WWW-Authenticate: NTLM AAAA` (an invalid Type-2), and `curl -sS --ntlm -u user:pass http://127.0.0.1:21004/ -w "[%{http_code}]"`.

**Expected (upstream curl).** `curl: (94) An authentication function returned an error`, the 401 headers printed, `[401 401 4]`, exit 94.

**Actual.** Same stderr and headers, but `[000 000 0]`, exit 94. `%{size_header}` is correct.

**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, `AnswerChallengesAsync` (about line 3265) rethrows `HttpAuthenticationFailedException` as `new HttpTransferException(failure.ExitCode, failure.Message)` without the 401 response already received. Carry the received response status and header count into the exception so the write-out reads them.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests/AuthenticationFailureWriteOutTests.cs` prove that a Digest challenge the client cannot answer (algorithm unsupported) gives exit 94 and a transfer result with response code 401 and the received header count, so `%{http_code}`, `%{response_code}` and `%{num_headers}` read `401 401 4` for a four-header response.
- [ ] A second test proves the NTLM invalid-Type-2 case reports response code 401.
- [ ] A regression test proves a failure before any response arrives still reports `000`.
- [ ] Tests are platform-neutral (in-memory connection stand-ins; no SSPI needed: use a stand-in authenticator that throws `HttpAuthenticationFailedException`).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: step 2 prints `[401 401 4]` and exits 94; step 3 prints `[401]`.

## Log

- 2026-11-13: Created.
