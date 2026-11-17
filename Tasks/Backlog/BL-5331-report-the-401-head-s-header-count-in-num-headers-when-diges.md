---
id: BL-5331
title: Report the 401 head's header count in %{num_headers} when Digest fails with exit 94
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/DigestFailureNumHeadersTests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5331 — Report the 401 head's header count in %{num_headers} when Digest fails with exit 94

## Goal

When Digest authentication fails with exit 94, `%{num_headers}` is the number of header lines in the last 401 response head, not 0, as curl reports.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-068 (Low, Confirmed twice), re-reported with status CHANGED ("`%{http_code}` and `%{response_code}` now fixed"): the earlier fix is incomplete. Earlier task: BL-5118 (report the 401 in `-w` variables when Digest or NTLM authentication fails), in `Tasks/Done`; see also `Cgr135NumHeadersTests.cs` in `Curl.Original.Protocol.Http.UnitTests`. Area: HTTP Digest, write-out, Windows.

**Repro.** A server on 127.0.0.1:31000 that always answers `401` with `WWW-Authenticate: Digest realm="r@x", nonce="dcd98b7102dd2f0e8b11d0f600bfb0c093", algorithm=SHA-256, qop="auth"` and `Content-Length: 4` (PowerShell or C# file-based app, never Python). `curl -sS -i --digest -u user:pass http://127.0.0.1:31000/d256 -w "[%{http_code} %{response_code} %{num_headers}]"`

**Expected (reference curl).** `[401 401 2]`, `curl: (94) An authentication function returned an error`, exit 94.

**Actual (port).** `[401 401 0]`, same error and exit.

**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, `AnswerChallengesAsync` (about line 3298) rethrows `new HttpTransferException(failure.ExitCode, failure.Message)` without the 401 head's header lines. Follow what BL-5118 did for the status code and carry the head's header count the same way.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests/DigestFailureNumHeadersTests.cs` (in-memory connection stand-in answering 401 with the Digest challenge above) prove the transfer fails with `CurlExitCode` 94 and its report carries the response code 401 and a header count of 2 (the number `Cgr135NumHeadersTests.cs` shows the write-out reads).
- [ ] A test proves the same count when the challenge is NTLM or the failure is another auth exit, if BL-5118's path covers it, and that a successful Digest exchange still reports its final response's count.
- [ ] Tests are platform-neutral (stand-ins, no real network).
- [ ] Hand check, recorded in the Log: the repro command prints `[401 401 2]` and exits 94; measure real curl with `Record-CurlExchange.ps1` and say which version.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the command prints `[401 401 2]` and exits 94.

## Log

- 2026-11-16: Created.
