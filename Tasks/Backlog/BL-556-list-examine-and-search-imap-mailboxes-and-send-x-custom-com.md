---
id: BL-556
title: List, examine and search IMAP mailboxes and send -X custom commands
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-555]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-556 — List, examine and search IMAP mailboxes and send -X custom commands

## Goal

`imap://host/` sends `LIST`, `imap://host/<mailbox>` without a UID does what curl 8.21.0 does (`EXAMINE` or `SELECT` and its output), `imap://host/<mailbox>?<criteria>` sends `SEARCH`, and `-X <command>` is sent as curl sends it, each writing the untagged responses curl writes.

## Context

- Conformance audit 2026-09-28, row 34.
- Measure with `Record-CurlExchange.ps1 -Imap`: `imap://h/`, `imap://h/INBOX`, `imap://h/INBOX?NEW`, `-X "EXAMINE INBOX" imap://h/`, `-X "STORE 1 +FLAGS \Seen" imap://h/INBOX`, and `SEARCH` answered `BAD`; record request lines and stdout bytes.

## Acceptance criteria

- [ ] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Imap.UnitTests` pin client bytes, output bytes and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
