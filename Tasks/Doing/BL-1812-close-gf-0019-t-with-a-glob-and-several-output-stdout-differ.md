---
id: BL-1812
title: Close GF-0019: -T with a glob and several --output: stdout differs from upstream's %EMPTY expectation
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1812 — Close GF-0019: -T with a glob and several --output: stdout differs from upstream's %EMPTY expectation

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0019 (-T with a glob and several --output: stdout differs from upstream's %EMPTY expectation), so a later gap analysis measures each of `behaviour:test2013`, `behaviour:test2014` as `match`.

## Context

- Finding: GF-0019, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test2013`, `behaviour:test2014`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both items expect 'upstream test<N> passes'. test2013 actual: <verify><stdout> differs at byte 0 (line 1): expected '%EMPTY', got the end. test2014 has the same shape. Curl wrote nothing to stdout. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 2013,2014

Suggestion, copied from the finding:

First confirm against the reference curl what test2013/2014 write to stdout with -T '{1,2}' and --output per transfer (Curl.Console's output routing). If the reference also writes nothing, the difference is the harness not reading upstream's %EMPTY. If the reference writes something, route the upload responses that have no --output left to stdout as curl does.

## Acceptance criteria

- [ ] `behaviour:test2013`: Curl answers what curl 8.21.0 answers, `upstream test2013 passes`, so the item measures `match`.
- [ ] `behaviour:test2014`: Curl answers what curl 8.21.0 answers, `upstream test2014 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
