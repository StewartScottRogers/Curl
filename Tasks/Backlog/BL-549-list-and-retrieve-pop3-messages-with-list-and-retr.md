---
id: BL-549
title: List and retrieve POP3 messages with LIST and RETR
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-547]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-549 — List and retrieve POP3 messages with LIST and RETR

## Goal

`pop3://host/` sends `LIST` and writes the listing, and `pop3://host/<n>` sends `RETR <n>` and writes the message with dot-unstuffing and without the terminating `.` line, exactly as curl 8.21.0 writes them, with `-ERR` answers mapped to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 34. URL model: BL-533's ADR.
- Measure with `Record-CurlExchange.ps1 -Pop3` (`-Pop3Message` with a stuffed `..` line and CRLF endings): `pop3://h/`, `pop3://h/1`, `pop3://h/9` answered `-ERR no such message`, and a message split so the terminator straddles two reads (the handler test covers the split; measurement covers the output bytes). Record stdout bytes exactly.
- Download progress and `%{size_download}` follow what curl counts (measure with `-w '%{size_download}'`).

## Acceptance criteria

- [ ] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Pop3.UnitTests` pin output bytes and outcome for each case, and the terminator split across reads.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
