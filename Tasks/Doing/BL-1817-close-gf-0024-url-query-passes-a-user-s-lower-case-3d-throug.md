---
id: BL-1817
title: Close GF-0024: --url-query passes a user's lower-case %3d through instead of normalising it to %3D
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1817 — Close GF-0024: --url-query passes a user's lower-case %3d through instead of normalising it to %3D

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0024 (--url-query passes a user's lower-case %3d through instead of normalising it to %3D), so a later gap analysis measures each of `behaviour:test1221` as `match`.

## Context

- Finding: GF-0024, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1221`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1221 expected 'upstream test1221 passes' (the reference curl exits 0), actual: <verify><protocol> differs at byte 131 (line 1): expected '...&%3D%3D HTTP/1.1', got '...&%3d%3d HTTP/1.1'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1221

Suggestion, copied from the finding:

In Curl.Protocol.Abstractions.UnitLibrary's CurlUrl query building (used by --url-query in Curl.Cli.UnitLibrary), upper-case the hex digits of existing percent escapes when the query is appended, as curl's urlapi does with CURLU_APPENDQUERY.

## Acceptance criteria

- [ ] `behaviour:test1221`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
