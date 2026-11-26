---
id: BL-6020
title: Fix fitness finding STD-223: FTP %{num_headers} is 1 when no server reply contains a colon; curl reports 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpHeadHeaderLines.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpNumHeadersTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6020 — Fix fitness finding STD-223: FTP %{num_headers} is 1 when no server reply contains a colon; curl reports 0

## Goal

STD-223 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: FTP %{num_headers} is 1 when no server reply contains a colon; curl reports 0. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Low · **Confidence:** Confirmed (Windows and Linux builds against REF and REF2) · **Area:** FTP/FTPS write-out (regression from the BL-5403 fallback).
- **Server:** `docker run -d --name vsftpd -p 127.0.0.1:25222:21 -p 127.0.0.1:25240-25249:25240-25249 -e PASV_MIN=25240 -e PASV_MAX=25249 -e FTPS=off curlreview/vsftpd:1` (user `test`/`test`; vsftpd 3.0.3's replies contain no colon).
- **Repro:** `curl -sS -u test:test -o /dev/null -w "%{num_headers}\n" ftp://127.0.0.1:25222/hello.txt` (also with `-T loc.txt ftp://127.0.0.1:25222/up1.txt`, and `ftp://127.0.0.1:25222/`).
- **Expected (REF 8.22.0 and REF2 8.18):** `0` for all three (curl counts only reply lines containing a colon: pure-ftpd's "Local time is now 13:07." gives 1; vsftpd gives 0).
- **Actual (.NET, both platforms):** `1` for all three, exit 0.
- **Likely location:** `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs`, `PseudoHeadersOf`: the branch `counted.Length > 0 ? counted : … : [FtpHeadHeaderLines.TransferComplete]` invents one line when no reply has a colon.
- **Acceptance check:** the repro prints `0` against vsftpd, and a server whose replies contain a colon (pure-ftpd, pyftpdlib) still prints its count.

## Acceptance criteria

- [x] the repro prints `0` against vsftpd, and a server whose replies contain a colon (pure-ftpd, pyftpdlib) still prints its count.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Reproduced and measured 2026-11-26 with `Record-CurlExchange.ps1 -Ftp`, whose default replies
  hold no colon (as vsftpd's): curl 8.22.0 prints `0` for a download, an `-T` upload and a
  listing, `1` when only the final reply holds a colon (`226 done: ok`), and `3` for `-I` of a
  file answering `SIZE` and `MDTM`. The `1` BL-5403 pinned came from a colon reply, not a rule.
- Fix: `FtpSession.CountedPseudoHeaders` counts the colon reply lines and the `-I` lines only;
  the stand-in `226 Transfer complete` fallback is gone. No ADR: curl's measured behaviour.
- `touches` widened with `FtpHeadHeaderLines.cs` (its `TransferComplete` had no other user) and
  `FtpNumHeadersTests.cs`; no task in Doing names either.
- Checks: no Docker vsftpd in a lane, so the repro ran as `Record-CurlExchange.ps1 -Ftp` sessions
  with colon-free replies (vsftpd's shape): the built `Curl.Console` printed `0`, `0`, `0`, and `1`
  with one colon reply and `3` for `-I`, the same as real curl. Build clean (0 warnings); fast tests
  green (one `FtpProtocolHandlerConnectOnlyTests` record comparison failed on the first run because
  an empty count was a new array; fixed, FTP tests 1373 passed, 9 skipped; every other assembly had
  passed). `Measure-CodeQuality.ps1` for the FTP library: 0 failing members. Platform-neutral.
- `TransferReport.TransferCompleteStandInHeader` and the engine's removal of it are now unused;
  `ConnectionEndPointRecorder.ProxyConnect.cs` is held by BL-6019 in Doing, so BL-6056 removes them.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: a0832679 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. FTP %{num_headers} counts only reply lines holding a colon and -I lines, so a colon-free server gives 0 as curl 8.22.0 does
