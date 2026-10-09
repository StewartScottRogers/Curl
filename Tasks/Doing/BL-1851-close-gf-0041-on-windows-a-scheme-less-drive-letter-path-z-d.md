---
id: BL-1851
title: Close GF-0041: On Windows a scheme-less drive-letter path (Z:/dir/file) is parsed as host and port instead of a path under --proto-default file
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1851 — Close GF-0041: On Windows a scheme-less drive-letter path (Z:/dir/file) is parsed as host and port instead of a path under --proto-default file

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0041 (On Windows a scheme-less drive-letter path (Z:/dir/file) is parsed as host and port instead of a path under --proto-default file), so a later gap analysis measures each of `behaviour:test1146` as `match`.

## Context

- Finding: GF-0041, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1146`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measured: test1146 expected 'upstream test1146 passes', actual 'the --output file against <reply><data> differs at byte 0 (line 1): expected "foo\n", got the end'. The command is --proto-default file %PWD/%LOGDIR/test1146.txt; upstream expects exit 0 and the file's bytes. Reran on 6383c570 with the same result. Run directly, Curl.Console/bin/Debug/net10.0/curl.exe --proto-default file Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/t1146.txt prints 'curl: (3) URL rejected: Port number was not a decimal number between 0 and 65535' and exits 3. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 1146

Suggestion, copied from the finding:

In Curl.Core.UnitLibrary's UrlSchemeGuesser.HasScheme, on Windows, take a single ASCII letter followed by a colon and a forward or back slash as a drive prefix, not a scheme. AddScheme then gives it the --proto-default (or guessed) scheme, and the file handler reads 'file://Z:/dir/file' as the local path Z:/dir/file, as upstream test1146 expects (exit 0, the file's bytes as output). Pin it with an [OSCondition(OperatingSystems.Windows)] test in Curl.Core.UnitTests, and pin the off-Windows answer in another.

## Acceptance criteria

- [ ] `behaviour:test1146`: Curl answers what curl 8.21.0 answers, `upstream test1146 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
