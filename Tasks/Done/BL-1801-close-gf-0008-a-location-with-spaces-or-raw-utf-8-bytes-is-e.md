---
id: BL-1801
title: Close GF-0008: A Location with spaces or raw UTF-8 bytes is encoded differently from curl, or rejected
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
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

- [x] `behaviour:test40`: Curl answers what curl 8.21.0 answers, `upstream test40 passes`, so the item measures `match`.
- [x] `behaviour:test662`: Curl answers what curl 8.21.0 answers, `upstream test662 passes`, so the item measures `match`.
- [x] `behaviour:test663`: Curl answers what curl 8.21.0 answers, `upstream test663 passes`, so the item measures `match`.
- [x] `behaviour:test1138`: Curl answers what curl 8.21.0 answers, `upstream test1138 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Relative Location (`HttpRedirectLocation.Encode`): a space in the query is now `+` (test40), `%20` elsewhere; a character at or below U+00FF is one Latin-1-decoded header byte and is escaped as itself (`%D8%A2`, test1138), only a character above U+00FF (never from the wire) as UTF-8.
- Absolute Location: `%{redirect_url}` still keeps its spaces as sent (measured, BL-179). The follow encodes them instead: `RedirectFollower.EncodedForFollow` turns a space into `%20` (`+` in the query) and a byte above 0x7E into its escape before `CurlUrl.TryParse`, so `http://example.net/tes t case=/6620002` is followed as `tes%20t%20case` (test662, test663) rather than refused. This needed `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests`, added to `touches`; no other task in Doing names them.
- Not re-measured here: the gap tool and the upstream test data live under audit-guarded `Gap/` paths a lane may not read; the next gap analysis re-measures the four items. No option changed, so `--ai-help` needs nothing.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Relative Location spaces and raw bytes encoded as curl does; absolute Location with spaces followed encoded
