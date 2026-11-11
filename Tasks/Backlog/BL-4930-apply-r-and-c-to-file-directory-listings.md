---
id: BL-4930
title: Apply -r and -C to file:// directory listings
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-4902]
touches: [Curl.Original.Protocol.File.UnitLibrary/FileProtocolHandler.cs, Curl.Original.Protocol.File.UnitTests/FileDirectoryRangeTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4930 — Apply -r and -C to file:// directory listings

## Goal

Ranges and resume on a `file://` directory behave as in curl 8.22.0.

## Context

From the outside fitness review of 2026-11-11 (commit 6ce7cfcf, the `preview` `curl-win-x64.zip`, Windows 11 x64; REF = curl 8.22.0 Schannel), finding STD-020 (Low, confirmed: reproduced at least twice).

- Repro: `mkdir d2 && touch d2/alpha d2/beta`; with `U=file:///<abs>/d2/` run `curl -sS -r 0-2 $U`, `-C 3 $U`, `-r 3-7 $U`, `-r -3 $U`.
- Expected: REF 8.22.0: `-r 0-2` prints `alp`, exit 0; `-C 3` and `-r 3-7` print `curl: (36) failed to resume file:// transfer`, exit 36; `-r -3` prints `curl: (36) Could not resume download`, exit 36.
- Actual: All four print the full listing, exit 0.
- Where to start: `FileProtocolHandler.DownloadAsync` / `ListDirectoryAsync` / `WriteEntryNamesAsync` bypass the `TryResolveWindow` range check; treat the directory size as 0 for resume checks.

## Acceptance criteria

- [ ] The four commands give exactly REF's outputs and exit codes.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text, and add a focused unit test in the fixed library's own `.UnitTests` project.

## Log

- 2026-11-11: Created.
