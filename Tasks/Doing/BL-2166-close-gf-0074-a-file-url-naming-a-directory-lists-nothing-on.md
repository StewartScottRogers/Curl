---
id: BL-2166
title: Close GF-0074: A file:// URL naming a directory lists nothing on Windows, where curl 8.22.0 writes its entry names
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2166 — Close GF-0074: A file:// URL naming a directory lists nothing on Windows, where curl 8.22.0 writes its entry names

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0074 (A file:// URL naming a directory lists nothing on Windows, where curl 8.22.0 writes its entry names), so a later gap analysis measures each of `behaviour:test3203` as `match`.

## Context

- Finding: GF-0074, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3203`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test3203 (file://localhost%FILE_PWD/%LOGDIR/test3203.dir/ holding dir-listing-test.txt; expects exit 0 and stdout 'dir-listing-test.txt'): expected 'upstream test3203 passes', actual '<verify><stdout> differs at byte 0 (line 1): expected dir-listing-test.txt<LF>, got the end'. FileProtocolHandler.ListsDirectories (Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs line 149) is true only off Windows (OperatingSystem.IsWindows() negated), so on Windows a directory keeps exit 37; 8.22.0 changed test3203 (GF-0038) and no longer keeps it off Windows. Reproduce on Windows: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 3203

Suggestion, copied from the finding:

In Curl.Protocol.File.UnitLibrary, list directories on Windows as curl 8.22.0 does: once the Windows reference curl 8.22.0 is measured listing a directory with Record-CurlExchange.ps1, make FileProtocolHandler.ListsDirectories true on every platform (or remove it) and have the Windows file system implement IDirectoryLister with the same entry names and order as the Linux and macOS listing. Update the Windows-only tests that pin exit 37 for a directory.

## Acceptance criteria

- [ ] `behaviour:test3203`: Curl answers what curl 8.22.0 answers, `upstream test3203 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
