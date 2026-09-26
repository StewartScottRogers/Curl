---
id: BL-277
title: Sanitize glob-substituted -o names on Windows as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-207]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-277 — Sanitize glob-substituted -o names on Windows as curl does

## Goal

An `-o` name after `#N` substitution is sanitized on Windows as curl 8.21.0 sanitizes it (`sanitize_file_name` with `SANITIZE_ALLOW_PATH | SANITIZE_ALLOW_RESERVED`), so a glob value holding `?`, `*`, `"`, `<`, `>` or `|` gives the file name real curl writes.

## Context

- Filed from BL-207: `UrlGlobMatch.SubstituteGlobValues` substitutes `#N` exactly as `glob_match_url` does but stops before the Windows-only `sanitize_file_name` step that follows it in `tool_urlglob.c`.
- Measure against the Windows reference `/mingw64/bin/curl` (8.21.0), e.g. `curl -s -w "%{filename_effective}\n" -o "o_#1" "file:///n/{a?b,c*d,e:f}"`, record the commands and output in Notes, then pin them.
- Decide whether the step lives in `UrlGlobMatch` or in a separate sanitizer, and whether it runs only on Windows (curl runs it only on Windows and MS-DOS builds); record it in an ADR.

## Acceptance criteria

- [ ] Each character curl replaces or keeps in a substituted `-o` name on Windows is pinned in a test against measured curl 8.21.0 output.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

## Log

- 2026-09-26: Created.
