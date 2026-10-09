---
id: BL-1854
title: Close GF-0044: %PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1854 — Close GF-0044: %PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0044 (%PWD expands to a folder path with a blank, so the harness splits test3009's --output-dir argument and Curl fetches an extra URL), so a later gap analysis measures each of `behaviour:test3009` as `match`.

## Context

- Finding: GF-0044, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3009`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Measured: test3009 expected 'upstream test3009 passes', actual '<verify><protocol> differs at byte 94 (line 6): expected the end, got "GET /AppData/Local/Curl/gap/upstream/8.21.0/tests/not-there HTTP/1.1\r\n"'. The command is -O --output-dir %PWD/not-there, which upstream expects to fail with exit 23 after one request. %PWD is the release's tests folder under C:/Users/Stewart Rogers/AppData/Local, so 'Rogers/AppData/...' became a second URL. A copy of the case under a tests folder with no blank (Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/tests/data), rerun on 6383c570, passed. Reproduce from the repository root: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" Z:/repos/Curl.gap/2026-10-08_2029/scratch-gap-behaviour/raw.json 3009

Suggestion, copied from the finding:

Curl's behaviour needs no change: it passes test3009 once %PWD holds no blank. Make Curl.Conformance.UnitLibrary's UpstreamCaseRunner own %PWD: take it as a parameter and refuse one that holds a blank, as it already does for %LOGDIR. Pin the refusal in Curl.Conformance.UnitTests. The office's Measure-UpstreamCases.cs must then pass a tests folder with no blank, for example by copying the release under the run folder. That part is office work, filed interactively.

## Acceptance criteria

- [ ] `behaviour:test3009`: Curl answers what curl 8.21.0 answers, `upstream test3009 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
