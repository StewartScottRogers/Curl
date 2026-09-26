---
id: BL-011
title: Implement --create-file-mode on POSIX
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-009]
requirement: none
created: 2026-09-25
completed:
---
# BL-011 — Implement `--create-file-mode` on POSIX

## Goal

Files that `-o` or `--create-dirs` creates on a POSIX system get the octal mode given
by `--create-file-mode`.

## Context

POSIX only; a no-op on Windows. It depends on `PhysicalFileSystem` (BL-009) exposing
a create-mode parameter. Upstream behaviour: https://curl.se/docs/manpage.html,
`--create-file-mode`.

## Acceptance criteria

- [ ] `--create-file-mode` is parsed as an octal mode.
- [ ] On POSIX, files created for `-o` and `--create-dirs` get that mode.
- [ ] On Windows, the option is accepted and has no effect.
- [ ] Tests cover parsing and the mode passed through `IFileSystem`, without touching
      the disk.

## Notes

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
