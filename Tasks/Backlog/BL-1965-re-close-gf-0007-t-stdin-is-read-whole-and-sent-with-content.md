---
id: BL-1965
title: Re-close GF-0007: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-1965 — Re-close GF-0007: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0007 (-T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0), so a later gap analysis measures each of `behaviour:test60`, `behaviour:test98`, `behaviour:test1068`, `behaviour:test1069`, `behaviour:test1072`, `behaviour:test1073` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0007 ([BL-1800]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0007, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test60`, `behaviour:test98`, `behaviour:test1068`, `behaviour:test1069`, `behaviour:test1072`, `behaviour:test1073`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1068 actual: <verify><protocol> differs at byte 84 (line 5): expected 'Transfer-Encoding: chunked', got 'Content-Length: 19'. test1072/1073: expected 'Transfer-Encoding: chunked', got 'Content-Length: 122'. test60: expected 'Transfer-Encoding: chunked', got 'Content-Length: 1'. test98 (-H 'Transfer-Encoding:' -H 'Content-Length: 14'): expected 'Expect: 100-continue', got an empty line. test1069 (-T - -0): expected exit code 25, got 52. The reference curl exits 25 on test1072/1073 and 56 on test1068/test60. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1068,98,1069

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary (HttpRequestFraming, HttpRequestBodyWriter), treat a stdin upload as a body of unknown size. Over HTTP/1.1 frame it with Transfer-Encoding: chunked, even when the user gives a Content-Length header, unless the user empties Transfer-Encoding. Add Expect: 100-continue for an upload of unknown size. Over HTTP/1.0 (-0, or a 1.0 server after a redirect or auth retry) fail with exit 25, as curl's 'chunked transfer encoding not supported by HTTP/1.0'. Curl.Console's stdin reading must stream rather than buffer to give the unknown size.

## Acceptance criteria

- [ ] `behaviour:test60`: Curl answers what curl 8.21.0 answers, `reference curl exits 56; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test98`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1068`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1069`: Curl answers what curl 8.21.0 answers, `upstream test1069 passes`, so the item measures `match`.
- [ ] `behaviour:test1072`: Curl answers what curl 8.21.0 answers, `reference curl exits 25; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1073`: Curl answers what curl 8.21.0 answers, `reference curl exits 25; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
