---
id: BL-651
title: Parse --xattr and store the URL and content type as extended attributes where the platform allows
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-651 — Parse --xattr and store the URL and content type as extended attributes where the platform allows

## Goal

`--xattr` parses, and after a successful `-o`/`-O` transfer Curl stores the attributes the platform's curl 8.21.0 build stores (`user.xdg.origin.url`, `user.mime_type` and any others it writes) where that build does, and does exactly what the build does where it does not (the Windows build may ignore the option or write an alternate data stream: measure it).

## Context

- Conformance audit 2026-09-28, row 30 (Major; filed Low as rarely used).
- The BCL has no extended-attribute API; on Linux and macOS `setxattr` is a libc call, which would need `LibraryImport` (source-generated P/Invoke, AOT-safe, no package). Record the choice, and what is done per platform, in an ADR marked "Decided by Claude under Stewart's delegation".
- Output-file handling: `Curl.Console/OutputFileTarget.cs`; the `-R` file-time setter (`IFileTimeSetter`) is the model for a post-transfer file seam.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--xattr -o out.txt` with a `Content-Type`, on Windows and on Linux or macOS, and the attributes (or streams) found on `out.txt` afterwards, copied into Notes.
- [ ] Tests pin, per platform under `OSCondition`, the attribute names and values written through a fake seam.
- [ ] The ADR exists in `Documentation/Planning/Decisions` (number checked unused) and is indexed in its `README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
