---
id: BL-5130
title: Resend a file-backed upload on a fresh connection when a reused connection was closed
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/HttpRewindableUploadResendTests.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5130 — Resend a file-backed upload on a fresh connection when a reused connection was closed

## Goal

An HTTP/1.1 upload whose body is a file or file-backed MIME part (`-T file`, `-F name=@file`), sent on a reused connection the server has already closed, is rewound and resent on a fresh connection, as curl does, instead of failing with exit 52.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-053 (Medium, Confirmed; 3 runs each; curl 8.22.0 and 8.18.0 both succeed). The transfer fails where curl succeeds; with `-L` and 307/308 the upload never completes.

**Repro.**
1. Run a server on 127.0.0.1:20000 (PowerShell or a C# file-based app) that reads one request (head plus Content-Length body), answers with a Content-Length response that has **no** `Connection: close` header, then does `shutdown(SHUT_WR)` and closes. It answers `/status/307` with `307` and `Location: /redir-target`, everything else with `200`.
2. `echo a>u1.txt; echo b>u2.txt`
3. `curl -sS -T u1.txt http://127.0.0.1:20000/echo/a -T u2.txt http://127.0.0.1:20000/echo/b -o NUL -o NUL -w "%{url} %{http_code} %{num_connects}\n"; echo rc=$?`
4. `curl -sS -F f=@u1.txt http://127.0.0.1:20000/echo/a -F f=@u2.txt http://127.0.0.1:20000/echo/b -o NUL -o NUL; echo rc=$?`
5. `curl -sS -L -T u1.txt http://127.0.0.1:20000/status/307 -w "%{num_redirects} %{http_code}\n"; echo rc=$?`

**Expected.** Every transfer succeeds (`200 1` each) and every command exits 0. curl `-v` shows `* Connection died, retrying a fresh connect (retry count: 1)` and sends the PUT again on a new connection.

**Actual.** The second request fails with `curl: (52) Empty reply from server`, `-w` shows `000 0`, exit 52. `-d` bodies and GETs in the same position are resent correctly.

**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`, `CanSendAgainOnFreshConnection`: `plan.Framing.Body is not StreamBody { Length: not 0 }` refuses to resend any non-empty stream body. curl rewinds seekable bodies (files, MIME parts from files); allow the resend when the stream can be rewound (`Stream.CanSeek`, or the existing rewinder idea in `Curl.Console/AttemptUploadRewinder.cs`, which the run should read to learn how retries rewind uploads today). A non-seekable body (stdin) must still not be resent.

## Acceptance criteria

- [x] New tests in `Curl.Original.Protocol.Http.UnitTests/HttpRewindableUploadResendTests.cs` prove that, on a reused connection closed by the server after the first response (no `Connection: close`), a seekable `StreamBody` upload is resent in full on a new connection and succeeds (200, one connect for the resent request), for a PUT body and a multipart body from a file.
- [x] A test proves the same with a 307 redirect (`-L`): the upload is followed and completes.
- [x] A test proves a non-seekable stream body is still not resent (same failure as today, exit 52), and `-d`/GET resend behaviour is unchanged.
- [x] Tests are platform-neutral (in-memory connection stand-ins).
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a hand check: step 3 prints `http://127.0.0.1:20000/echo/a 200 1` and `http://127.0.0.1:20000/echo/b 200 1`, then `rc=0`; step 5 prints `1 200` and `rc=0`.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: e7a3fafb 6b4458c9 fb363b94 a8bf5a11 a0b49a29.
- 2026-11-13: Done. `CanSendAgainOnFreshConnection` now allows a stream body that can seek or implements `IUploadRewinder` (`BodyCanBeSentAgain`); `FailedOutcome` rewinds it through `RewindStreamUploadFailure` before the resend. Non-seekable bodies still fail with exit 52. The `-L` case is the engine running the handler again with a rewound body, tested at that level (the handler does not follow redirects itself). Replaced the old test `ReusedConnectionDiedAfterAStreamBody_FailsWithoutSendingAgain` (it used a seekable stream); the non-seekable case is in the new tests. Quality run: 0 failing members. Linux/macOS: tests are in-memory and platform-neutral. Unrelated: Curl.Networking BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext failed once in the full run (port collision with other lanes), not touched.
- 2026-11-13: Doing -> Done. Rewindable stream uploads are resent on a fresh connection
