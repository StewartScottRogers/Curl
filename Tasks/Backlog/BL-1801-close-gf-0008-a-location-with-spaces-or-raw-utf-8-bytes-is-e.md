---
id: BL-1801
title: Close GF-0008: A Location with spaces or raw UTF-8 bytes is encoded differently from curl, or rejected
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1801 — Close GF-0008: A Location with spaces or raw UTF-8 bytes is encoded differently from curl, or rejected

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0008 (A Location with spaces or raw UTF-8 bytes is encoded differently from curl, or rejected), so a later gap analysis measures each of `behaviour:test40`, `behaviour:test662`, `behaviour:test663`, `behaviour:test1138` as `match`.

## Context

- Finding: GF-0008, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test40`, `behaviour:test662`, `behaviour:test663`, `behaviour:test1138`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test40 (-L) actual: <verify><protocol> differs at byte 129 (line 6): expected 'GET /we/are/all/moo.html/?name=d+a+niel&testcase=/400002 HTTP/1.1', got 'GET /we/are/all/moo.html/?name=d%20a%20niel&testcase=/400002 HTTP/1.1' (a space in the query becomes +). test662/663 (Location 'http://example.net/tes t case=/6620002' through -x): expected 'GET http://example.net/tes%20t%20case=/6620002 HTTP/1.1', got the end (the redirect is not followed). test1138: expected '?name=%D8%A2%D8%BA...', got '?name=%C3%98%C2%A2...' (the raw bytes were read as Latin-1 and re-encoded as UTF-8). Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 40,662,1138

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpRedirectLocation (with CurlUrl in Curl.Protocol.Abstractions.UnitLibrary), resolve a Location as curl's urlapi does with CURLU_URLENCODE. Keep the header value as raw bytes, not decoded as Latin-1. Percent-encode each byte >= 0x80 as itself. Encode a space as %20 in the path and as + in the query. Accept an absolute Location that contains spaces instead of abandoning the redirect.

## Acceptance criteria

- [ ] `behaviour:test40`: Curl answers what curl 8.21.0 answers, `upstream test40 passes`, so the item measures `match`.
- [ ] `behaviour:test662`: Curl answers what curl 8.21.0 answers, `upstream test662 passes`, so the item measures `match`.
- [ ] `behaviour:test663`: Curl answers what curl 8.21.0 answers, `upstream test663 passes`, so the item measures `match`.
- [ ] `behaviour:test1138`: Curl answers what curl 8.21.0 answers, `upstream test1138 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-08: Created.
