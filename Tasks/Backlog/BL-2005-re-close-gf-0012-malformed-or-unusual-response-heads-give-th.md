---
id: BL-2005
title: Re-close GF-0012: Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2005 — Re-close GF-0012: Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0012 (Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)), so a later gap analysis measures each of `behaviour:test1144`, `behaviour:test1473`, `behaviour:test1480`, `behaviour:test2106` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0012 ([BL-1805]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0012, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1144`, `behaviour:test1473`, `behaviour:test1480`, `behaviour:test2106`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1144 (-I --http0.9, body only): <verify><errorcode> expected exit code 8, got 0. test1473 (first header line starts with a space): expected 0, got 8. test1480 (HTTP/1.1 100 Continue, then body with no final head): expected 8, got 1. test2106 (NUL byte in a chunked trailer): expected 8, got 0. The reference curl agrees with upstream on test1144, test1473, test1480 and test2106. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1144,1473,1480,2106

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary's HttpResponseHeadReader and HttpChunkedDecoder: (1) a HEAD (-I) answered by an HTTP/0.9 body is a weird server reply, exit 8; (2) fold, rather than reject, a first header line that starts with whitespace, as curl 8.21.0 does; (3) after a 1xx, bytes that do not start a status line are a weird server reply (8), not the HTTP/0.9 refusal (1); (4) a NUL byte in a trailer line fails with exit 8.

## Acceptance criteria

- [ ] `behaviour:test1144`: Curl answers what curl 8.21.0 answers, `reference curl exits 8; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1473`: Curl answers what curl 8.21.0 answers, `upstream test1473 passes`, so the item measures `match`.
- [ ] `behaviour:test1480`: Curl answers what curl 8.21.0 answers, `upstream test1480 passes`, so the item measures `match`.
- [ ] `behaviour:test2106`: Curl answers what curl 8.21.0 answers, `upstream test2106 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-10: Created.
