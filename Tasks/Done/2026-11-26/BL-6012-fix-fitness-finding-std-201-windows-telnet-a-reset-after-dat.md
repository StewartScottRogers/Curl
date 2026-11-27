---
id: BL-6012
title: Fix fitness finding STD-201: Windows TELNET: a reset after data was written still sometimes ends with exit 0 and loses the server's last line (PARTLY FIXED)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetOutbox.cs, Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerNegotiationResetTests.cs, Documentation/Planning/Decisions/ADR-1352-telnet-read-failing-during-option-negotiation-exits-56.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
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

- [x] the 20-run loop never prints `0/7`; every run is `0/22` or exits non-zero.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: Windows also exits 56 when a failed read strikes an unanswered upload (upload bytes sent in the session, and a reply or upload bytes sent since data was last written). Decided in ADR-1418; small enough that the feature stages ran in this session without subagents (cost cap).
- touches widened to the test file the criterion needs, the library's CLAUDE.md and ADR-1352's status line; no other task in Doing names them.
- Measured with a C# rebuild of the finding's mock (one program serving loopback and running the client 20 to 40 times). Server sends the last line then resets: curl 8.21.0 `0/21` x7, `55/0` x17, `56/7` x6; curl 8.22.0 `0/21` x3, `55/0` x12, `56/7` x10. Server resets 300 ms after `quit` sending nothing: both curls `0/7` every run.
- `0/21` is the finding's `0/22`: the whole output, which in this mock has a one-digit counter.
- Curl after the change (Debug build): sends-last-line `0/21` x6, `56/7` x34, never `0/7`; silent reset `56/7` x40, where curl gives `0/7`. A .NET read cannot tell the two apart, so ADR-1418 takes 56 over a success that may have lost data.
- The loop was not run on the pre-change build; the old rule returned exit 0 for any failed read after data was written, by construction.
- Gates: dotnet build 0 warnings; fast tests green; format clean for the two telnet projects; Measure-CodeQuality for the Telnet library: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-11-26: Created from fitness review 2026-11-26T0704.
- 2026-11-26: Backlog -> Doing. Criteria at claim: eb271d27 69116afe 3d836d5e.
- 2026-11-26: Doing -> Done. Windows TELNET reset on an unanswered upload now exits 56; the mock loop no longer reports 0/7
