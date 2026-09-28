---
id: BL-557
title: Upload an IMAP message with APPEND and --upload-flags
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-553]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-557 — Upload an IMAP message with APPEND and --upload-flags

## Goal

`-T <file> imap://host/<mailbox>` sends `APPEND <mailbox> (<flags>) {<n>}`, waits for the `+` continuation, sends the message, and maps a refusal to curl 8.21.0's exit code and message, with the flag list built from `--upload-flags` as curl builds it.

## Context

- Conformance audit 2026-09-28, rows 31 and 34. `--upload-flags` is parsed by BL-535 and carried on the context (BL-534).
- Measure with `Record-CurlExchange.ps1 -Imap`: `-T mail.txt imap://h/INBOX` with no flags, `--upload-flags seen,flagged`, `--upload-flags draft,-seen` (record what curl sends for an unset flag), `-T -` (unknown length: record whether curl refuses or buffers), and `APPEND` answered `NO`.

## Acceptance criteria

- [ ] Measured first as above; request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Imap.UnitTests` pin client bytes byte for byte and the outcome for each case; upload progress and `%{size_upload}` match the measured values.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
