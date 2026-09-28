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
completed:
---
# BL-665 — Bring Requirements.md up to date with the code, starting with its not-yet-implemented notes

## Goal

Every requirement row in `Documentation/Product/Requirements.md` says truthfully whether the behaviour is built: the 70 "Not yet implemented." notes are removed where the behaviour exists (FR-030 and FR-035 at least) and kept, naming the task, where it does not, and the header's TODO paragraph describes the document as it is.

## Context

- Conformance audit 2026-09-28, row 48 (Minor): 70 "Not yet implemented" notes, FR-030 (telnet unknown `-t` option, exit 48/49) and FR-035 (TFTP error packets to exits 68-74) are implemented.
- Check each row against the code and its tests (`Curl.Protocol.<Name>.UnitTests` names usually cite the FR), not against memory. A row whose behaviour is only partly built says which part is missing and which task covers it; file a task for a gap no task covers.
- The header paragraph lists which option groups have requirements; update it to the groups the document actually covers.

## Acceptance criteria

- [ ] `Select-String -Path Documentation/Product/Requirements.md -Pattern 'Not yet implemented'` finds only rows whose behaviour a named test shows is missing, each naming its task.
- [ ] FR-030 and FR-035 carry no not-implemented note and cite the tests that show them.
- [ ] The header TODO paragraph states the document's actual coverage.

## Notes

## Log

- 2026-09-28: Created.
