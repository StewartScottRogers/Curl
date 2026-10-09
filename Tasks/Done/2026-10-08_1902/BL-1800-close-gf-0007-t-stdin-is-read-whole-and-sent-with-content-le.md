---
id: BL-1800
title: Close GF-0007: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1800 — Close GF-0007: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0007 (-T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0), so a later gap analysis measures each of `behaviour:test60`, `behaviour:test98`, `behaviour:test1068`, `behaviour:test1069`, `behaviour:test1072`, `behaviour:test1073` as `match`.

## Context

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

- [x] `behaviour:test60`: Curl answers what curl 8.21.0 answers, `reference curl exits 56; stdout 0 bytes: `, so the item measures `match`. (Locally verified: stdin upload now chunked with Expect, exit 25 under -0; the next gap run measures it.)
- [x] `behaviour:test98`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`. (Locally verified: stdin upload now chunked with Expect, exit 25 under -0; the next gap run measures it.)
- [x] `behaviour:test1068`: Curl answers what curl 8.21.0 answers, `reference curl exits 56; stdout 0 bytes: `, so the item measures `match`. (Locally verified: stdin upload now chunked with Expect, exit 25 under -0; the next gap run measures it.)
- [x] `behaviour:test1069`: Curl answers what curl 8.21.0 answers, `upstream test1069 passes`, so the item measures `match`. (Locally verified: stdin upload now chunked with Expect, exit 25 under -0; the next gap run measures it.)
- [x] `behaviour:test1072`: Curl answers what curl 8.21.0 answers, `reference curl exits 25; stdout 0 bytes: `, so the item measures `match`. (Locally verified: stdin upload now chunked with Expect, exit 25 under -0; the next gap run measures it.)
- [x] `behaviour:test1073`: Curl answers what curl 8.21.0 answers, `reference curl exits 25; stdout 0 bytes: `, so the item measures `match`. (Locally verified: stdin upload now chunked with Expect, exit 25 under -0; the next gap run measures it.)
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured before the change: with standard input a pipe, Curl's `-T -` already sent
  `Transfer-Encoding: chunked` and `Expect: 100-continue`, and under `-0` exited 25 (`Chunky
  upload is not supported by HTTP 1.0`); `HttpRequestFraming` treats any unseekable upload as of
  unknown size. The gap arose only when standard input could seek (a file redirected into it,
  as the gap tool feeds `<stdin>`), when `HttpUploadResume.Of` took its length and sent
  `Content-Length`.
- Decision (Stewart's standing rule, match curl): curl 8.21.0 never sizes standard input, so
  `-T -` / `-T .` now reach the handler through `StandardInputUploadStream`, read-only and never
  seekable. No HTTP library change was needed, so `Curl.Protocol.Http.*` stays untouched. test98
  (`Expect` with an emptied `Transfer-Encoding` and a `-H Content-Length`) follows from the same
  unknown length (`WantsExpect` for a null length).
- Not re-measured with the gap tool (lanes may not read `Gap/`); the next gap run closes or
  reopens the items. No option changed, so `--ai-help` is unchanged.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. -T - now reaches the handler as an unseekable StandardInputUploadStream, so stdin uploads go chunked with Expect: 100-continue and exit 25 under -0, as curl 8.21.0; the gap closes on the next gap run
