---
id: BL-1669
title: Cap the telnet subnegotiation buffer at curl's 512 bytes so a server sending SB without SE cannot grow memory without limit
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Telnet.UnitLibrary, Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1669 — Cap the telnet subnegotiation buffer at curl's 512 bytes so a server sending SB without SE cannot grow memory without limit

## Goal

A telnet server that sends `IAC SB` and then any number of bytes without `IAC SE` holds Curl's memory to a fixed bound, as curl's does, instead of growing it with every byte.

## Context

- Found by BL-1519's adversarial attacks. `TelnetReceiver` (`Curl.Protocol.Telnet.UnitLibrary/TelnetReceiver.cs`) adds every subnegotiation byte to its `subnegotiation` list, with no limit, until `IAC SE` arrives. A hostile server that opens `IAC SB 05` and streams forever grows that list until the process runs out of memory.
- curl 8.21.0's `lib/telnet.c` keeps the subnegotiation in `unsigned char subbuffer[SUBBUFSIZE]` (`SUBBUFSIZE` 512); `CURL_SB_ACCUM` drops each byte once the buffer is full, so the rest of the subnegotiation is read and discarded. Only `subbuffer[0]` (the option) decides the answer, so the bytes sent and written do not change; `printsub` for `-v`/`--trace` shows only what was kept.
- Fix: stop adding bytes once the subnegotiation holds 512 (check the exact count against `CURL_SB_ACCUM` and `CURL_SB_TERM`, which reserve room for the terminating `IAC SE`), and trace only what was kept.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Telnet.UnitTests` feeds `IAC SB 05` followed by 1,000,000 bytes, streamed through a scripted connection in reads of at most 4096 bytes, then `IAC SE` and one data byte, and checks the session ends exit 0 with only that data byte written; the subnegotiation the trace reports is at most 512 bytes long.
- [ ] A `TTYPE SEND` subnegotiation padded past 512 bytes is still answered with the `-t TTYPE` value (the existing `ExecuteAsync_TerminalTypeSubnegotiationLongerThanCurlsBuffer_IsAnsweredOnce` stays green).
- [ ] `dotnet build` is clean, the fast tests pass, and `Curl.Protocol.Telnet.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
