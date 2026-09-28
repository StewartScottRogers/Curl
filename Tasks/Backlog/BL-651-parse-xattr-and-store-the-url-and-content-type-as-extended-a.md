---
id: BL-651
title: Parse --xattr and store the URL and content type as extended attributes on every OS curl can
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-651 — Parse --xattr and store the URL and content type as extended attributes on every OS curl can

## Goal

`--xattr` parses, and after a successful `-o`/`-O` transfer Curl stores the attributes curl 8.21.0 stores (`user.xdg.origin.url`, `user.mime_type` and any others it writes) on every operating system for which any curl build writes them (read `src/tool_xattr.c` at tag `curl-8_21_0` for the list), whatever the platform's usual build does; only on an operating system no curl build supports does Curl do what curl does there.

## Context

- Conformance audit 2026-09-28, row 30 (Major; filed Low as rarely used). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): what any official curl build supports, Curl supports on every platform it can exist on; the BCL lacking an API is not a reason to leave it out.
- The BCL has no extended-attribute API; on Linux and macOS `setxattr` (and FreeBSD `extattr_set_file` if relevant) is a libc call through `LibraryImport` (source-generated P/Invoke, part of the BCL, AOT-safe, no package); if curl writes attributes on Windows (for example as NTFS alternate data streams), the BCL's file APIs can write those. Record the route per OS in an ADR marked "Decided by Claude under Stewart's delegation" (HOW, not WHETHER).
- Output-file handling: `Curl.Console/OutputFileTarget.cs`; the `-R` file-time setter (`IFileTimeSetter`) is the model for a post-transfer file seam.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--xattr -o out.txt` with a `Content-Type`, on Windows and on Linux or macOS, and the attributes (or streams) found on `out.txt` afterwards, copied into Notes.
- [ ] Tests pin, per platform under `OSCondition`, the attribute names and values written through a fake seam.
- [ ] The ADR exists in `Documentation/Planning/Decisions` (number checked unused) and is indexed in its `README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
