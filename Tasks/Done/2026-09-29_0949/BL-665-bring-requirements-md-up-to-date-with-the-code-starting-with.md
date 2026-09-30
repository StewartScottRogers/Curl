---
id: BL-665
title: Bring Requirements.md up to date with the code, starting with its not-yet-implemented notes
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-665 — Bring Requirements.md up to date with the code, starting with its not-yet-implemented notes

## Goal

Every requirement row in `Documentation/Product/Requirements.md` says truthfully whether the behaviour is built: the 70 "Not yet implemented." notes are removed where the behaviour exists (FR-030 and FR-035 at least) and kept, naming the task, where it does not, and the header's TODO paragraph describes the document as it is.

## Context

- Conformance audit 2026-09-28, row 48 (Minor): 70 "Not yet implemented" notes, FR-030 (telnet unknown `-t` option, exit 48/49) and FR-035 (TFTP error packets to exits 68-74) are implemented.
- Check each row against the code and its tests (`Curl.Protocol.<Name>.UnitTests` names usually cite the FR), not against memory. A row whose behaviour is only partly built says which part is missing and which task covers it; file a task for a gap no task covers.
- The header paragraph lists which option groups have requirements; update it to the groups the document actually covers.

## Acceptance criteria

- [x] `Select-String -Path Documentation/Product/Requirements.md -Pattern 'Not yet implemented'` finds only rows whose behaviour a named test shows is missing, each naming its task.
- [x] FR-030 and FR-035 carry no not-implemented note and cite the tests that show them.
- [x] The header TODO paragraph states the document's actual coverage.

## Notes

- Select-String is case-insensitive, so it matched 79 notes, not 70 (lower-case "(not yet implemented)" forms and section intros included). 77 removed, each replaced with "Built; shown by `Class.Method`"; all 185 cited test names were checked to exist in the class named.
- Kept 2: FR-013 (`--crlf` parsed but never copied to the transfer context, BL-633) and FR-073 (the `zstd` part of `--compressed`, BL-861).
- FR-030 cites `TelnetProtocolHandlerTelnetOptionTests` (exit 48 and 49 cases); FR-035 cites `TftpProtocolHandlerTests.ExecuteAsync_ErrorPacket_ReturnsCurlsExitCodeAndMessage` (rows 0 to 9) and two `TftpUploadTests`.
- Header TODO paragraph is now a "Coverage" note naming the groups covered and what the code implements without requirements. Section intros that said nothing was wired now name the class doing the work.
- Gaps with no task filed: BL-908 (FR-098 says any `-b` turns on the cookie engine; code and BL-237 measurement say only a `-b` file or `-c`), BL-909 (FR-064 and FR-042 cases untested, FR-085 wording stale).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Requirements.md rows say truthfully what is built, citing tests; only FR-013 (BL-633) and FR-073 zstd (BL-861) remain not implemented
