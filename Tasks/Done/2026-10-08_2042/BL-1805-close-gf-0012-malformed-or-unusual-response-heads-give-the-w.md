---
id: BL-1805
title: Close GF-0012: Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1805 — Close GF-0012: Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0012 (Malformed or unusual response heads give the wrong exit code (HTTP/0.9 under -I, header starting with whitespace, 100 without a final response, NUL in a trailer)), so a later gap analysis measures each of `behaviour:test1144`, `behaviour:test1473`, `behaviour:test1480`, `behaviour:test2106` as `match`.

## Context

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

- [x] `behaviour:test1144`: Curl answers what curl 8.21.0 answers, `reference curl exits 8; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1473`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1480`: Curl answers what curl 8.21.0 answers, `reference curl exits 8; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test2106`: Curl answers what curl 8.21.0 answers, `reference curl exits 8; stdout 0 bytes: `, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Fixed in Curl.Protocol.Http.UnitLibrary: (1) `HttpResponseHeadReader.IsHeadRequest` (set from `context.NoBody`, i.e. `-I`) makes an HTTP/0.9 answer accepted by `--http0.9` fail with exit 8 (test1144); (2) `HttpResponseHeadBuilder` takes a continuation line with no header before it as a header of its own with its leading blanks dropped, as curl's `Curl_headers_push` does, so exit 0 (test1473); (3) after a 1xx head, bytes that cannot begin a status line fail with exit 8 instead of the HTTP/0.9 refusal, exit 1 (test1480); (4) `HttpChunkedDecoder` fails a trailer line holding a NUL byte with exit 8 (test2106).
- Default taken: the stderr text for (1) and (3) is `Invalid status line` and for (4) `Nul byte in header`, the messages Curl already uses for those kinds of failure. The lane cannot read the upstream test data (the audit guard refuses the gap cache path), so the exit codes come from the finding and the texts are not measured; the gap analysis measures exit code and stdout.
- No option changed, so `--ai-help` needs nothing.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Four malformed-head cases now exit as curl 8.21.0 does; build clean, fast tests green
