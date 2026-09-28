---
id: BL-555
title: Fetch an IMAP message with SELECT and FETCH from the URL's mailbox, UID and section
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-553]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-555 — Fetch an IMAP message with SELECT and FETCH from the URL's mailbox, UID and section

## Goal

`imap://host/<mailbox>;UID=<n>` (with `;UIDVALIDITY=`, `;SECTION=` and `;PARTIAL=` as curl reads them, and `;MAILINDEX=` where curl 8.21.0 accepts it) sends `SELECT` then the `FETCH`/`UID FETCH` curl sends, writes the literal's bytes, and maps a UIDVALIDITY mismatch and a `NO` to curl's exit codes and messages.

## Context

- Conformance audit 2026-09-28, row 34. URL model: BL-533's ADR and RFC 5092 (IMAP URL), as curl reads it.
- Measure with `Record-CurlExchange.ps1 -Imap`: `INBOX;UID=1`, `INBOX;MAILINDEX=1`, `INBOX;UID=1;SECTION=TEXT`, `INBOX;UID=1;PARTIAL=0.10`, `INBOX;UIDVALIDITY=2;UID=1` against `UIDVALIDITY 1`, a mailbox name needing quoting (`My Box`), and `FETCH` answered `NO`; record request lines and stdout bytes.

## Acceptance criteria

- [ ] Measured first as above; request lines, stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Imap.UnitTests` pin client bytes, output bytes and outcome for each case.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
