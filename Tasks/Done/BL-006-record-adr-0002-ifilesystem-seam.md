---
id: BL-006
title: Record ADR-0002 - IFileSystem, not IConnection, as the seam for the file scheme
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
requirement: none
created: 2026-09-25
completed: 2026-09-25
---
# BL-006 — Record ADR-0002: `IFileSystem`, not `IConnection`, as the seam for the `file` scheme

## Goal

The decision that the `file` scheme's handler receives `IFileSystem` rather than
`IConnection` is recorded as an Architecture Decision Record.

## Context

`Documentation/Planning/Decisions/ADR-0002-ifilesystem-as-the-second-protocol-seam.md`.
The work also amended `Curl.Protocol.File.UnitLibrary/CLAUDE.md` and Rule 2 in
`Product/Product-Overview.md`.

## Acceptance criteria

Not recorded. This task was finished before the task board existed.

## Notes

Migrated from the `Done` table of `Documentation/Planning/Backlog.md`.

## Log

- 2026-09-25: Done. Also amends Curl.Protocol.File.UnitLibrary/CLAUDE.md and Rule 2 in Product/Product-Overview.md.
- 2026-09-25: Migrated from Documentation/Planning/Backlog.md when the task board was created.
