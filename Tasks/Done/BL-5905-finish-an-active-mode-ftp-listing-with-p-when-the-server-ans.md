---
id: BL-5905
title: Finish an active-mode FTP listing with -P when the server answers slowly (upstream test 251)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpActiveCommand.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpPortArgument.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpActiveCommand, Curl.Original.Protocol.Ftp.UnitLibrary/FtpControlChannel.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpActiveCommandSlowReplyTests.cs, Curl.Original.Protocol.Ftp.UnitTests/Fakes/ScriptedConnection.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5905 — Finish an active-mode FTP listing with -P when the server answers slowly (upstream test 251)

## Goal

`curl ftp://<host>/ -P <address>` against a server that answers slowly sends `PWD`, `PORT`, `TYPE A`, `LIST`, `QUIT`, completes the listing and exits 0, as upstream curl 8.21.0 does, so upstream test case 251 ("FTP dir list, PORT with specified IP and slow response") passes.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 24 (Surl finding UTC-4 in `Upstream-Test-Cases.md`), measured on win-x64 at port commit `74f3f45` (2026-11-16) beside upstream curl 8.21.0 Schannel. Check it still reproduces on the current branch first.

Command: `curl ftp://127.0.0.1:<port>/ -P 127.0.0.1` against test 251's FTP server half with `SLOWDOWN` (the server sends each reply byte by byte with a delay). Upstream logs in, sends `PWD`, `PORT`, `TYPE A`, `LIST`, `QUIT` and exits 0. The port did not finish within the case's 60 s limit on all three runs. Likely the active-mode accept or the reply reader times out or waits on a reply split across many reads; earlier active-mode hangs were BL-1978 and BL-464. Start at `FtpActiveCommand.cs`. What decides: RFC 959 section 3.2 and the case's `<protocol>` section.

## Acceptance criteria

- [x] Upstream test case 251 passes with the port in its upstream-case runner.
- [x] A focused unit test in `Curl.Original.Protocol.Ftp.UnitTests` with a test double that drips each reply slowly (fake `TimeProvider`, no sleeps) pins the completed listing and the command order (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced on this branch with a loopback FTP server that drips each reply byte by byte and connects the data socket after the `150`: Curl showed `< 2d26 Directory send OK` and ended with exit 28 `FTP response timeout`; Windows' curl.exe finished with exit 0.
- Cause, in `FtpControlChannel`, not in `FtpActiveCommand`: the read `WaitForBytesAsync` starts while the data connection is accepted stayed pending after its bytes had been taken, so the next fill returned at once and one stale buffer byte entered the reply. A garbage byte at the start of a line loses the `226`; a zero byte is exit 8. The fix forgets that read as soon as a byte is consumed.
- `touches` widened to `FtpControlChannel.cs`, the new test file and `Fakes/ScriptedConnection.cs` (an `OnRead` hook that moves the fake clock); no other task in Doing named them.
- Case 251 was already on `PassingUpstreamCases.txt` and passes (`CURL_UPSTREAM_CASE=251`, and in the fast tests): the harness's FTP stand-in ignores `SLOWDOWN`, so it never met the bug. Filed BL-5924 for that.
- No ADR: a defect fix, no behaviour decision.
- Measure-CodeQuality.ps1 for the FTP library: 100% line and branch, 0 failing members. Fast tests green.

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 24.
- 2026-11-24: Backlog -> Doing. Criteria at claim: a9d78d40 76c28db7 3d836d5e.
- 2026-11-24: Doing -> Done. Active-mode FTP under -P completes with a slow-replying server: the control channel no longer reuses an already delivered pending read.
