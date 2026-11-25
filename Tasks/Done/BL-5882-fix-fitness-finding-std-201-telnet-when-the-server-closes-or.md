---
id: BL-5882
title: Fix fitness finding STD-201: TELNET: when the server closes or resets during option negotiation, the port drops the data it already received and exits 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitLibrary/TelnetOutbox.cs, Curl.Original.Protocol.Telnet.UnitLibrary/CLAUDE.md, Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerNegotiationResetTests.cs, Curl.Original.Protocol.Telnet.UnitTests/Fakes/FaultingConnection.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5882 — Fix fitness finding STD-201: TELNET: when the server closes or resets during option negotiation, the port drops the data it already received and exits 0

## Goal

STD-201 from the outside fitness review of 2026-11-24 13:58 no longer reproduces: TELNET: when the server closes or resets during option negotiation, the port drops the data it already received and exits 0. Curl behaves as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-24T1358` (commit `1e5d591`, the `preview` release binaries). That review was a deeper second pass of the same commit as `Curl-Fitness-Review-2026-11-24T0708`, covering areas the earlier pass did not test.

In the finding, .NET is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Measure real curl with `Record-CurlExchange.ps1` before pinning output text.

The reviewer's mock servers were Python, and some findings used real servers in Docker (Dovecot, GreenMail, Samba, Mosquitto, Kafka, Prosody, SeaweedFS, Azurite); rebuild any mock you need in PowerShell or C# (never Python). Mock servers bind 127.0.0.1 and port numbers are examples. Check that it still reproduces on the current branch first.

The finding, verbatim:

- **Severity:** Medium (data loss with a success exit code) · **Confidence:** Confirmed (port lost output in 7 of 8 runs, repeated in separate runs) · **Area:** TELNET, Windows build.
- **Mock server:** a TELNET server on 127.0.0.1:36120 that sends `IAC DO TTYPE`, `IAC DO NEW-ENVIRON`, `IAC DO NAWS`, `IAC DO XDISPLOC`, waits 0.3 s, sends the matching `IAC SB … SEND` requests plus `login: `, then reads the client's data; when it sees `quit` it sends `\r\nbye N\r\n\xff\xffff\r\n` and closes the socket (reviewer script `work/d6/misc.py telnet 36120`).
- **Repro** (`tq.txt` contains `hello quit`):
  `for i in 1 2 3 4 5 6 7 8; do curl -s -m 6 -t TTYPE=vt100 -t XDISPLOC=host:0 -t NEW_ENV=USER,bob -T tq.txt telnet://127.0.0.1:36120 -o out.bin -w '%{exitcode}/%{size_download} '; done`
- **Expected (REF 8.22.0):** `0/22 56/7 56/7 0/22 55/0 56/7 56/7 55/0` (timing-dependent): every run either writes the received bytes (`login: `, plus `bye…` when exit is 0) or exits non-zero (55 or 56).
- **Actual (.NET):** `0/0 0/22 0/0 0/0 0/0 0/0 0/0 0/0` — exit 0 with nothing written. With `-v` it prints `* Sending data failed (10053)` and still exits 0 with no output, although `login: ` had arrived before the failed send.
- **Likely location:** `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs` and `TelnetOutbox.cs`. The doc comment says a failed read "ends the session like a close, with exit 0"; data queued by `ReportDataReceived` before the failing send or read is not flushed to the output, and a send failure is swallowed. Flush received data before ending, and return 55 (send failed) / 56 (recv failed) as curl does.
- **Acceptance check:** the 8-run loop never prints `0/0`; every run shows `size_download` ≥ 7 or a non-zero exit code.

## Acceptance criteria

- [x] the 8-run loop never prints `0/0`; every run shows `size_download` ≥ 7 or a non-zero exit code.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Touches widened with the library CLAUDE.md, the new test file and its fake; the only other Doing task naming Telnet on origin/work/dark-factory was this one.
- Reproduced on the current branch (debug build): `0/7` with exit 0 after `Sending data failed (10053)`, and `0/18`; no `0/0` in 15 runs, but a read failing before `login: ` is written gave exit 0 with nothing.
- curl 8.23.0 Windows, same mock: `0/18`, `56/7 Recv failure: Connection was reset`, `55/0`. A reset 300 ms after `login: ` was read (with or without `DO TTYPE`): exit 0.
- Fix (ADR-1347): a failed read exits 56 when a reply failed to send, or replies went and no data was written yet; otherwise exit 0 as before. curl 55 for a failed upload was already matched.
- Acceptance loop after the fix, 3 x 8 runs with a PowerShell mock: only `56/7` and `0/18`; never `0/0`.
- Measure-CodeQuality for the Telnet library: 0 failing members. Full solution build clean, fast tests green. No command-line option changed, so `--ai-help` is untouched.
- Linux and macOS behaviour is filed as BL-5918; the new tests are platform-neutral.

## Log

- 2026-11-24: Created from fitness review 2026-11-24T1358.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 3db95686 69116afe 3d836d5e.
- 2026-11-24: Doing -> Done. A TELNET reset during option negotiation now exits 56 instead of exit 0 with data lost
