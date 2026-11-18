---
id: BL-5472
title: Fix fitness finding STD-151: DICT, GOPHER and TELNET report %{time_pretransfer} and %{time_posttransfer} as 0 and %{time_starttransfer} equal to %{time_total}
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs, Curl.Original.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs, Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5472 — Fix fitness finding STD-151: DICT, GOPHER and TELNET report %{time_pretransfer} and %{time_posttransfer} as 0 and %{time_starttransfer} equal to %{time_total}

## Goal

STD-151 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: DICT, GOPHER and TELNET report %{time_pretransfer} and %{time_posttransfer} as 0 and %{time_starttransfer} equal to %{time_total}. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** dict://, gopher://, telnet:// write-out, Windows (remainder of STD-097)
- **Repro:** a DICT server on 127.0.0.1:44628 (greeting `220 ready`; `CLIENT` → `250 ok`; any other command → `150 1 definitions\r\n151 "x" db "Db"\r\nhello\r\n.\r\n250 ok\r\n`; `QUIT` → `221 bye`); a TCP server on 127.0.0.1:44170 that reads one line, sends `hello gopher\r\n.\r\n` and closes. `W` as in STD-150.
  - `curl -sS -m 5 -o NUL -w "$W" dict://127.0.0.1:44628/d:x`
  - `curl -sS -m 3 -o NUL -w "$W" gopher://127.0.0.1:44170/0x`
  - `echo x | curl -sS -m 3 -o NUL -w "$W" telnet://127.0.0.1:44170`
- **Expected (REF):** DICT `c=0.000657 pre=0.000705 post=0.000705 st=0.001355 tot=0.001817`; Gopher `pre=0.000751 post=0.000751 st=0.000736 tot=0.001971`; TELNET `pre=0.103077 post=0.103077 st=0.102997 tot=0.103266`.
- **Actual (PORT):** DICT `c=0.002352 pre=0.000000 post=0.000000 st=0.003331 tot=0.003331`; Gopher and TELNET the same pattern.
- **Likely location:** `StampTimings` in `Curl.Original.Protocol.Dict.UnitLibrary/DictProtocolHandler.cs:139`, `Curl.Original.Protocol.Gopher.UnitLibrary/GopherProtocolHandler.cs:155`, `Curl.Original.Protocol.Telnet.UnitLibrary/TelnetProtocolHandler.cs:185` — each calls `new TransferTimings(started, connect, null, null, completed, completed)`. Pass pretransfer/posttransfer when the request is sent and the first byte received as starttransfer.
- **Acceptance check:** the three commands print non-zero `pre` and `post` with `c ≤ pre`, and `st` < `tot`.

## Acceptance criteria

- [ ] the three commands print non-zero `pre` and `post` with `c ≤ pre`, and `st` < `tot`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-151 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 8f8f0b4c 69116afe 3d836d5e.
