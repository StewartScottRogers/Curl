---
id: BL-1983
title: Close GF-0053: An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1983 — Close GF-0053: An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0053 (An FTP reply holding a NUL byte or control characters (in PWD's path) is accepted; curl ends with exit 8), so a later gap analysis measures each of `behaviour:test3217`, `behaviour:test3218`, `behaviour:test2108` as `match`.

## Context

- Finding: GF-0053, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test3217`, `behaviour:test3218`, `behaviour:test2108`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test3217/3218 (PWD answered '257' with a path holding byte 0x03): '<verify><protocol> differs at byte 43 (line 4): expected the end, got "EPSV\r\n"'; upstream expects exit 8. test2108 (PASS answered '230 logged' NUL ' in'): expected the end, got 'PWD'; upstream expects exit 8. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 3217,3218,2108

Suggestion, copied from the finding:

In Curl.Protocol.Ftp.UnitLibrary's control-reply reader, fail with exit 8 (weird server reply) on a reply line holding a NUL byte. In the PWD reply parser, refuse a path holding control characters with exit 8, sending nothing more, as curl 8.21.0 does.

## Acceptance criteria

- [ ] `behaviour:test3217`: Curl answers what curl 8.21.0 answers, `upstream test3217 passes`, so the item measures `match`.
- [ ] `behaviour:test3218`: Curl answers what curl 8.21.0 answers, `upstream test3218 passes`, so the item measures `match`.
- [ ] `behaviour:test2108`: Curl answers what curl 8.21.0 answers, `upstream test2108 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
