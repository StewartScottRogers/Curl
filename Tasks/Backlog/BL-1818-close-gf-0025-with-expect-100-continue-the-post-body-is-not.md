---
id: BL-1818
title: Close GF-0025: With Expect: 100-continue, the POST body is not sent when the server answers early and closes
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1818 — Close GF-0025: With Expect: 100-continue, the POST body is not sent when the server answers early and closes

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0025 (With Expect: 100-continue, the POST body is not sent when the server answers early and closes), so a later gap analysis measures each of `behaviour:test1070` as `match`.

## Context

- Finding: GF-0025, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test1070`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test1070 (-d @file -H 'Expect: 100-continue') expected 'upstream test1070 passes', actual: <verify><protocol> differs at byte 176 (line 9): expected 'This creates ', got the end. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1070

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpContinueWaitConnection / HttpRequestBodyWriter: when the Expect wait times out or the server's final response has not yet arrived, start sending the body as curl 8.21.0 does. Then the bytes upstream's <verify><protocol> holds are written before the server's close is seen.

## Acceptance criteria

- [ ] `behaviour:test1070`: Curl answers what curl 8.21.0 answers, `upstream test1070 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
