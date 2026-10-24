---
id: BL-3285
title: Port proto_servers.py's SMTP, POP3 and IMAP servers to the differential runner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3284]
touches: [Curl.Conformance.Differential.UnitLibrary/Protocols/Mail, Curl.Conformance.Differential.UnitTests/Protocols/Mail]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3285 — Port proto_servers.py's SMTP, POP3 and IMAP servers to the differential runner

## Goal

SMTP (18210), POP3 (18220) and IMAP (18230) responders port `proto_servers.py`'s `Smtp`, `Pop3` and `Imap` classes byte for byte, including the AUTH continuations and the DATA and APPEND log lines.

## Context

Reference scripts: `Z:\repos\Curl.difftest\2026-10-22\proto_servers.py` (read only, never run Python). Parent: BL-3234. Reuse BL-3284's line reader, shared wire log and CliProtocols host; responders over a `Stream` in the UnitLibrary, one file per server, tested with in-memory streams (ADR-0782).

## Acceptance criteria

- [x] SMTP, POP3 and IMAP responders answer every command the Python classes handle with the same reply bytes and wire log lines, each covered by unit tests.
- [x] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [x] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3234 by its lane-3 run on 2026-10-23.
- Responders only (SmtpResponder, Pop3Responder, ImapResponder, MailMessages); a Python crash (bad number, index past the list, missing literal size) ends the connection. IMAP reads through DifferentialHttpLineReader directly because APPEND needs a counted read ProtocolLineReader lacks. Registering them on the CliProtocols host is left to the host task. Measure-CodeQuality: 0 failing members, 352 fast tests pass; platform-neutral (in-memory streams only).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a60fbd14 ae951636 5e81fa96.
- 2026-10-23: Doing -> Done. SMTP, POP3 and IMAP responders port proto_servers.py byte for byte with unit tests
