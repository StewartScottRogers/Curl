---
id: BL-429
title: Match curl's X| to X: rewrite in FileUrlPath on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-429 — Match curl's X| to X: rewrite in FileUrlPath on Windows

## Goal

On Windows, `FileUrlPath` treats a `/X|…` drive the way the curl 8.21.0 Schannel build does, measured first: `file:///c|/Windows/win.ini` opens `c:\Windows\win.ini` if real curl opens it, and the exit 37 message quotes whatever real curl quotes.

## Context

- Found during BL-428. The curl 8.21.0 source (`lib/file.c`, `file_connect`, inside `#ifdef DOS_FILESYSTEM`) does `actual_path[2] = ':'; actual_path++;` when `actual_path[0] == '/'`, `actual_path[1]` is non-NUL and `actual_path[2]` is `:` or `|`. So it rewrites `|` to `:` in the path it opens, and it does not require `actual_path[1]` to be a letter.
- `Curl.Protocol.File.UnitLibrary/FileUrlPath.cs` says "the `|` spelling is kept, because curl does not translate it", and `FileUrlPathTests` expects `OsPath` `c|\Windows\win.ini` for `file:///c|/Windows/win.ini` (Windows-only test). The exit 37 message may quote the untranslated URL path (from the parsed URL, not `real_path`), so `UrlPath` may rightly keep `|` while `OsPath` must not.
- Measure real curl on Windows with `Record-CurlExchange.ps1` before changing anything: `curl -sS -o NUL file:///c|/Windows/win.ini` (exit code, stderr), and one missing file with `|` to see the quoted text.
- `CurlUrl` (Abstractions) already rejects a non-letter drive; do not change `Curl.Protocol.Abstractions.UnitLibrary`.

## Acceptance criteria

- [ ] The measurement (command, exit code, stderr) is recorded in the task's Notes or a test comment.
- [ ] A Windows-only test in `FileUrlPathTests` pins `OsPath` and `UrlPath` for `file:///c|/Windows/win.ini` to the measured behaviour, and the `FileUrlPath` remarks state it.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green and `FileUrlPath` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
