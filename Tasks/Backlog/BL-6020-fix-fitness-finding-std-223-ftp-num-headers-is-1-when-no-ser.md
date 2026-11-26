---
id: BL-6020
title: Fix fitness finding STD-223: FTP %{num_headers} is 1 when no server reply contains a colon; curl reports 0
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs]
requirement: none
created: 2026-11-26
completed:
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

- [ ] the repro prints `0` against vsftpd, and a server whose replies contain a colon (pure-ftpd, pyftpdlib) still prints its count.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
