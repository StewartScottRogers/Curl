---
id: BL-1810
title: Close GF-0017: --compressed mishandles 'Content-Encoding: none' and a broken deflate header
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1810 — Close GF-0017: --compressed mishandles 'Content-Encoding: none' and a broken deflate header

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0017 (--compressed mishandles 'Content-Encoding: none' and a broken deflate header), so a later gap analysis measures each of `behaviour:test223`, `behaviour:test328` as `match`.

## Context

- Finding: GF-0017, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test223`, `behaviour:test328`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both items expect 'upstream test<N> passes'. test328 (Content-Encoding: none): the --output file against <reply><data> differs at byte 119 (line 7): expected 'Q- What did 0 say to 8? A- Nice Belt!', got the end. test223 (broken deflate header): stderr differs from the reference curl, which exits 61. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 223,328

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpContentCoding / HttpContentDecoder: treat 'none' (like 'identity') as no coding and pass the body through. For a deflate stream with a bad header, fail with exit 61 and curl 8.21.0's exact message ('Error while processing content unencoding: ...').

## Acceptance criteria

- [ ] `behaviour:test223`: Curl answers what curl 8.21.0 answers, `reference curl exits 61; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test328`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
