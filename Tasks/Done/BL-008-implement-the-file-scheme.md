---
id: BL-008
title: Implement the file scheme - FileProtocolHandler behind IFileSystem
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-006, BL-007]
requirement: none
created: 2026-09-25
completed: 2026-09-25
---
# BL-008 — Implement the `file` scheme: `FileProtocolHandler` behind `IFileSystem`

## Goal

`Curl.Protocol.File.UnitLibrary` handles the `file` scheme for GET and PUT, with
ranges and resume as seeks, returning the same exit codes as upstream curl.

## Context

See `Documentation/Planning/Decisions/ADR-0002-ifilesystem-as-the-second-protocol-seam.md`.
The handler takes `IFileSystem`, not `IConnection`, and every test in
`Curl.Protocol.File.UnitTests` runs against a fake, with no disk access.
Exit codes: https://curl.se/libcurl/c/libcurl-errors.html, checked against
curl 8.21.0.

## Acceptance criteria

- [x] `FileProtocolHandler` handles GET and PUT through `IFileSystem` only.
- [x] `-r`/`--range` and `-C`/`--continue-at` are implemented as seeks.
- [x] A failure to open the source for reading returns exit 37
      (`CURLE_FILE_COULDNT_READ_FILE`); a failure to open the destination for
      writing returns exit 23 (`CURLE_WRITE_ERROR`).
- [x] Every test in `Curl.Protocol.File.UnitTests` uses a fake `IFileSystem`, and
      none carries `[TestCategory("Integration")]`.
- [x] `conformance-auditor` reports no Blocker findings.

## Notes

Uncommitted skeletons already exist: `FileProtocolHandler.cs` and `FileUrlPath.cs` in
`Curl.Protocol.File.UnitLibrary`, plus `IFileSystem` and its supporting types in
`Curl.Protocol.Abstractions.UnitLibrary`. The two `file` types throw
`NotImplementedException` on purpose, as the targets for tests written first. Start
from them rather than from scratch. No tests had been written yet.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-25: Backlog -> Doing.
- 2026-09-25: Implemented behind IFileSystem per ADR-0002. 121 tests in Curl.Protocol.File.UnitTests, 32 in Curl.Protocol.Abstractions.UnitTests, none Integration. Two review rounds and two conformance audits against curl 8.21.0: the 5 review Must-fixes, 2 conformance Blockers and a signed-overflow defect in the bounded-range arithmetic are all fixed and mutation-checked. Remaining gaps filed as BL-015 to BL-027; BL-009, BL-010, BL-013, BL-021 and BL-024 amended.
- 2026-09-25: Doing -> Done. The file scheme reads and writes local files through IFileSystem: 121 tests, no Blocker findings against curl 8.21.0.
