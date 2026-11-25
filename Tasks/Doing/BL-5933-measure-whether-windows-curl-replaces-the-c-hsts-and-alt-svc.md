---
id: BL-5933
title: Measure whether Windows curl replaces the -c, --hsts and --alt-svc files by rename and match it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/FileSystem, Curl.Core.UnitTests/FileSystem]
requirement: none
created: 2026-11-24
completed:
---
# BL-5933 — Measure whether Windows curl replaces the -c, --hsts and --alt-svc files by rename and match it

## Goal

On Windows, saving the `-c` jar, the `--hsts` file and the `--alt-svc` file leaves each file as the Windows reference curl leaves it.

## Context

BL-5888 made the three files be replaced by rename off Windows and left Windows writing in place, unmeasured (ADR-1368). Measure the reference curl on Windows over an existing file, a read-only file, a hard-linked file and a file another process holds open (compare the NTFS file ID, attributes and content before and after), then match it in `Curl.Core.UnitLibrary/FileSystem/PhysicalFileSystem.cs` or record that it writes in place.

## Acceptance criteria

- [ ] An ADR records what the Windows reference curl does to each of the three files in the four cases.
- [ ] Tests in `Curl.Core.UnitTests` marked `[OSCondition(OperatingSystems.Windows)]` pin Curl's matching behaviour.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: ed061db0 15eca511 3d836d5e.
