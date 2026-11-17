---
id: BL-5390
title: Report DICT, Gopher and TELNET transfer timings and download speed in write-out
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Dict.UnitLibrary, Curl.Original.Protocol.Dict.UnitTests, Curl.Original.Protocol.Gopher.UnitLibrary, Curl.Original.Protocol.Gopher.UnitTests, Curl.Original.Protocol.Telnet.UnitLibrary, Curl.Original.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-5390 — Report DICT, Gopher and TELNET transfer timings and download speed in write-out

## Goal

`%{time_connect}`, `%{time_starttransfer}`, `%{time_total}` and `%{speed_download}` for DICT, Gopher and TELNET transfers are non-zero, as curl reports them.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-097 (Low, Confirmed by the orchestrator, 2 runs each: write-out values only). Area: dict://, gopher://, telnet:// write-out, Windows.

The same gap affects the Part B messaging extension protocols (EXT-083, BL-5410), so the fix may belong in a shared engine path for non-HTTP handlers. Coordinate with BL-5410 before writing three copies of timing code; if a shared engine fix is chosen, widen `touches` to the engine files it changes.

**Repro.**
1. A DICT server on 127.0.0.1:41628: greeting `220 ready`, answers `CLIENT` with `250 ok`, any other command with `150 1 definitions\r\n151 "x" db "Db"\r\nhello\r\n.\r\n250 ok\r\n`, `QUIT` with `221 bye`.
2. A TCP server on 127.0.0.1:41070 that reads one line, sends `hello gopher\r\n.\r\n` and closes.
3. `W='%{time_connect} %{time_starttransfer} %{time_total} %{size_download} %{speed_download}\n'`
   - `curl -sS -m 5 -o NUL -w "$W" dict://127.0.0.1:41628/d:x`
   - `curl -sS -m 3 -o NUL -w "$W" gopher://127.0.0.1:41070/0x`
   - `echo x | curl -sS -m 3 -o NUL -w "$W" telnet://127.0.0.1:41070`

**Expected (REF).** Non-zero times, e.g. `0.000704 0.001529 0.001708 88 53789` (DICT), `0.000684 0.000741 0.001677 17 10732` (Gopher), `0.000621 0.107644 0.107990 17 157` (TELNET).
**Actual (PORT).** `0.000000 0.000000 0.000000 88 0`, `0.000000 0.000000 0.000000 17 0`, `0.000000 0.000000 0.000000 17 0`. Sizes are correct.
**Where to start.** `DictProtocolHandler.cs`, `GopherProtocolHandler.cs` and `TelnetProtocolHandler.cs` return a `TransferReport` without `Timings` (none of the three files mentions `Timings`). Compare how SMB (`SmbProtocolHandler.StampReport`) and FILE (`FileProtocolHandler.WithTimings`) stamp them.

## Acceptance criteria

- [ ] New tests in each of `Curl.Original.Protocol.Dict.UnitTests`, `Curl.Original.Protocol.Gopher.UnitTests` and `Curl.Original.Protocol.Telnet.UnitTests` (a new file each) prove the `TransferReport` carries timings with `time_connect`, `time_starttransfer` and `time_total` set from an injected `TimeProvider` that advances, so they are non-zero.
- [ ] Tests prove download speed is non-zero when bytes and elapsed time are both non-zero.
- [ ] Tests prove the already-correct `size_download` is unchanged.
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] Hand check recorded in the Log: the three commands print non-zero `time_connect`, `time_starttransfer`, `time_total` and `speed_download`. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: the three commands print non-zero `time_connect`, `time_starttransfer`, `time_total` and `speed_download`.
- Related: EXT-083 (BL-5410), the same gap in the messaging extension protocols.

## Log

- 2026-11-17: Created.
