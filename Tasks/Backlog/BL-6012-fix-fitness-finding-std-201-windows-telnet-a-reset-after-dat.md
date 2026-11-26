---
id: BL-6012
title: Fix fitness finding STD-201: Windows TELNET: a reset after data was written still sometimes ends with exit 0 and loses the server's last line (PARTLY FIXED)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetOutbox.cs, Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6012 — Fix fitness finding STD-201: Windows TELNET: a reset after data was written still sometimes ends with exit 0 and loses the server's last line (PARTLY FIXED)

## Goal

STD-201 from the outside fitness review of 2026-11-26 07:04 no longer reproduces: Windows TELNET: a reset after data was written still sometimes ends with exit 0 and loses the server's last line (PARTLY FIXED). Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-26T0704` (commit `23b62c2`, the `preview` release binaries).

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer used Python mocks and real servers in Docker (pinned images listed in the review workspace's TestServers.md); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (silent truncation with a success exit code) · **Confidence:** Confirmed (port gave `0/7` in 5 of about 40 runs; REF never, in about 32) · **Area:** TELNET, Windows build.
- **What is fixed:** the old acceptance check passes (no run writes nothing with exit 0).
- **Mock server:** on `127.0.0.1:26120`: sends `IAC DO TTYPE`, `DO NEW-ENVIRON`, `DO NAWS`, `DO XDISPLOC`, waits 0.3 s, sends the `SB … SEND` requests and `login: `, then reads; on `quit` it sends `\r\nbye N\r\n\xff\xffff\r\n` and closes while client data is still unread, so the close becomes a reset (reviewer script `misc_d6.py telnet 26120`). `tq.txt` contains `hello quit`.
- **Repro:** `for i in $(seq 1 20); do curl -s -m 6 -t TTYPE=vt100 -t XDISPLOC=host:0 -t NEW_ENV=USER,bob -T tq.txt telnet://127.0.0.1:26120 -o t.bin -w '%{exitcode}/%{size_download} '; done`
- **Expected (REF 8.22.0):** only `0/22`, `56/7` or `55/0` — when the reset destroys the `bye` data after `login: ` was written, curl exits 56.
- **Actual (.NET):** `0/22`, `56/0` and also `0/7`: exit 0 with only `login: ` written; the `bye …` line is missing. The trace shows `<= Recv data, 7 bytes`, then `* shutting down connection #0`.
- **Likely location:** `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetOutbox.cs`, `FailedReadIsReceiveError` (`!runsOnWindows || writeFailed || (writeTried && bytesWritten == 0)`), with `TelnetProtocolHandler.FailedReadResult`: on Windows a reset after any byte was written becomes success. ADR-1352 calls this deliberate ("curl on Windows sees a reset of an idle session as the socket's close event"), but curl 8.22 returns 56 when the reset arrives while the session is active.
- **Acceptance check:** the 20-run loop never prints `0/7`; every run is `0/22` or exits non-zero.

## Acceptance criteria

- [ ] the 20-run loop never prints `0/7`; every run is `0/22` or exits non-zero.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
