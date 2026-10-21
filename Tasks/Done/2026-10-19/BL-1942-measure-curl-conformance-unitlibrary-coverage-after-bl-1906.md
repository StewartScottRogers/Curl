---
id: BL-1942
title: Measure Curl.Conformance.UnitLibrary coverage after BL-1906 and close any gap in FtpTransferCommands and FtpDataConnection
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-19
---
# BL-1942 — Measure Curl.Conformance.UnitLibrary coverage after BL-1906 and close any gap in FtpTransferCommands and FtpDataConnection

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] This task is obsolete, and closes without a change: it measured coverage after BL-1906, whose work BL-1956 did, and BL-1956 is Done.

## Notes

- 2026-10-19: Stewart had it closed as obsolete. Its criteria were replaced by the one above while it was in the Backlog, and the evidence was checked mechanically first.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Blocked. Stewart: BL-1942 measures FtpTransferCommands and FtpDataConnection, which exist only in BL-1906's uncommitted stash, and BL-1906 depends-on BL-1942 - a cycle; commit BL-1906's code (or drop its depends-on BL-1942) so this can measure it.
- 2026-10-10: Blocked -> Deferred. Folded into BL-1906, which now measures its own coverage; the depends-on cycle between them is broken
- 2026-10-19: Deferred -> Backlog. Stewart: close it as obsolete (it measured coverage after BL-1906, whose work BL-1956 did, and BL-1956 is Done).
- 2026-10-19: Backlog -> Doing. Interactive session: closing it as obsolete. Criteria at claim: d3d3bc4a.
- 2026-10-19: Doing -> Done. Obsolete: it measured coverage after BL-1906, whose work BL-1956 did, and BL-1956 is Done.
