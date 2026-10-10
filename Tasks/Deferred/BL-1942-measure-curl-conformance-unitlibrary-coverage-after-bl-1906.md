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
completed:
---
# BL-1942 — Measure Curl.Conformance.UnitLibrary coverage after BL-1906 and close any gap in FtpTransferCommands and FtpDataConnection

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Blocked. Stewart: BL-1942 measures FtpTransferCommands and FtpDataConnection, which exist only in BL-1906's uncommitted stash, and BL-1906 depends-on BL-1942 - a cycle; commit BL-1906's code (or drop its depends-on BL-1942) so this can measure it.
- 2026-10-10: Blocked -> Deferred. Folded into BL-1906, which now measures its own coverage; the depends-on cycle between them is broken
