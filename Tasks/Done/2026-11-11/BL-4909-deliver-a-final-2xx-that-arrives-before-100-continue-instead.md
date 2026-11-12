---
id: BL-4909
title: Deliver a final 2xx that arrives before 100 Continue instead of discarding it
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpContinueWaitConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpRequestBodyWriter.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs, Curl.Original.Protocol.Http.UnitTests/EarlyFinalResponseDuringContinueWaitTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4909 — Deliver a final 2xx that arrives before 100 Continue instead of discarding it

## Goal

When the server answers the request head with a final 2xx before `100 Continue`, the response is written and the exit code comes from the send (55) or 0 on a clean close.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-005 (Medium, confirmed: reproduced at least twice).

- Repro: Server on 18200 that reads only the request head, sends `HTTP/1.1 200 OK`, `Content-Length: 5`, `Connection: close`, body `fast\n`, waits 0.5 s and closes. A 2,000,000-byte `big.bin`. `curl -sS -T big.bin -w "%{http_code}\n" http://127.0.0.1:18200/a2xx` (also `--data-binary @big.bin`).
- Expected: REF 8.22.0: stdout `fast\n200\n`, stderr empty, exit 0, at once. Without `Connection: close`: `fast\n200\n` then `curl: (55) Send failure: Connection was aborted`.
- Actual: Waits the full 1 s, sends the body, prints `000`, `curl: (56) Recv failure: Connection was reset`, exit 56. Early 3xx/4xx/5xx are handled correctly.
- Where to start: `HttpContinueWaitConnection.WhenWaitEndsAsync` / `StopsSending`. Continuing to send on an early 2xx is right (CGR-067 / BL-3596); the bug is that the status line and body read during the wait are thrown away when the send then fails.

## Acceptance criteria

- [x] The repro prints `fast` then `200` and exits 0.
- [x] Without `Connection: close` it prints `fast`, `200` and exits 55 with REF's message.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

2026-11-11 lane 9: blocked before any code change. `dotnet build Curl.Console` fails with
"SDK not found": `global.json` pins 10.0.401 (rollForward latestFeature), and
`C:\Program Files\dotnet\sdk` now holds only 10.0.100-rc.1.25451.107, 11.0.100-rc.2.26504.105
and 11.0.100 (installed 2026-11-10 17:13, probably removing the 10.0.4xx SDK). No lane can
build or run tests until a 10.0.4xx SDK is reinstalled.
What the code reading found for the next claim: `HttpContinueWaitConnection.ReadStatusLineAsync`
buffers only up to the first line feed. After an early 2xx, the wait runs out, the body is sent,
and the server's close turns into a reset, which on Windows drops the unread rest of the head and
body, so the next read fails with 56 and `000`. A likely fix, inside the touched file: after a
final 2xx, keep reading the response into the replay buffer during the wait and the send, so it
can still be delivered when the send fails. Exit 0 or 55 as REF reports then follows from whether
the head says `Connection: close` (that may need `HttpRequestBodyWriter`/`HttpProtocolHandler`
too). Measure REF first: the WinGet curl here is 8.18.0 mingw/LibreSSL and is not the reference
build. Scripts for `Record-CurlExchange.ps1 -Script` are: read / send head+`fast\n` / pause 500 / close.

2026-11-11 lane 4: done. A send failure (exit 55) after a final 2xx answered the wait for
`100 Continue` is now held (`HttpProtocolHandler.SendBodyBefore2xxAsync`) while the response is
read and delivered, then thrown unless that response says `Connection: close`
(`ThrowIfSendFailedUnlessClosing`, using the new `HttpConnectionPersistence.NamesConnectionClose`).
`HttpContinueWaitConnection.AnsweredWithFinal2xx` tells the two apart from 3xx and above, which
still stop sending. `HttpRequestBodyWriter` no longer reads the connection for a receive failure
after a rate or source wait once a 2xx answered (that 1-byte read took a byte of the response).
Touches widened to HttpProtocolHandler.cs, HttpRequestBodyWriter.cs and
HttpConnectionPersistence.cs: no task in Doing on origin/work/dark-factory names them.
Measured with Curl and `Record-CurlExchange.ps1 -Script` (read / send head+`fast\n` / pause 500 /
close) and a 2,000,000-byte `-T` file: with `Connection: close` stdout `fast\n200\n`, stderr empty,
exit 0; without it stdout `fast\n200\n`, `curl: (55) Send failure: Connection was aborted`, exit 55,
the same as REF 8.22.0 in the review. No ADR: the behaviour is REF's as the review measured it, no
choice was made. Quality for Curl.Original.Protocol.Http.UnitLibrary: 100% line, 100% branch,
0 failing members (coverage of the Http.UnitTests run; the -Library run aborts on Console tests,
below). The fast tests are green apart from six Windows SSPI NTLM tests in
Curl.Authentication.UnitTests, Curl.Original.Protocol.Ldap.UnitTests and Curl.Console.UnitTests
that fail on this host and touch no HTTP code: filed as BL-4978. The new tests are
platform-neutral (the send failure's words come from `CurlSocketErrorText.Words` for the platform).

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 572c4f19 12d3e7a4 a0b49a29.
- 2026-11-11: Doing -> Blocked. Stewart: the .NET SDK 10.0.4xx that global.json pins (10.0.401, latestFeature) is no longer installed (only 10.0.100-rc.1 and 11.0.100); please reinstall it so lanes can build.
- 2026-11-11: Blocked -> Backlog. SDK 10.0.404 installed 2026-11-11 ~09:20 Arizona; global.json resolves. Any work a lane kept is in that lane's stash - apply by hash, never pop.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 572c4f19 12d3e7a4 a0b49a29.
- 2026-11-11: Doing -> Done. A final 2xx before 100 Continue is delivered: exit 0 with Connection: close, else 55 after the response, as curl 8.22.0
