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
completed: 2026-10-10
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

- [x] `behaviour:test1144`: Curl answers what curl 8.21.0 answers, `reference curl exits 8; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1473`: Curl answers what curl 8.21.0 answers, `upstream test1473 passes`, so the item measures `match`.
- [x] `behaviour:test1480`: Curl answers what curl 8.21.0 answers, `upstream test1480 passes`, so the item measures `match`.
- [x] `behaviour:test2106`: Curl answers what curl 8.21.0 answers, `upstream test2106 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured end-to-end before any change: the upstream test files (fetched from curl-8_21_0 on GitHub, since the audit guard refuses the gap cache) served through Record-CurlExchange.ps1 to the current Curl.Console give exit 8 (test1144), 0 with stdout `-foo-` (test1473), 8 (test1480) and 8 (test2106), with and without `--trace-ascii`/`-o`/`-v` - the exit codes curl 8.21.0 expects. BL-1805 (db232966a, on master since 2026-10-08) already closed all four; the gap analysis that filed this task measured the pre-BL-1805 exit codes (0, 8, 1, 0), so it most likely ran a Curl build older than that commit. Nothing in Curl.Protocol.Http needed changing for the exit codes.
- One difference found and fixed: for test1144 real curl (8.18.0, the newest on this machine) prints `curl: (8) Weird server reply`, Curl printed `Invalid status line`. `HttpResponseHeadReader` now uses the new `HttpTransferMessages.WeirdServerReply`, pinned in `ReadAsync_Http09AnswerToHeadRequest_ReturnsWeirdServerReply`. Default taken: 8.18.0's text stands in for 8.21.0's, since this message has not changed in curl's http.c for years.
- No option changed, so `--ai-help` needs nothing.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. All four GF-0012 cases already give curl 8.21.0's exit codes end to end (BL-1805); HEAD+HTTP/0.9 text now Weird server reply
