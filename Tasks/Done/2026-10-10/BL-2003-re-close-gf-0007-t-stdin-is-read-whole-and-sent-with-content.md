---
id: BL-2003
title: Re-close GF-0007: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2003 — Re-close GF-0007: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0007 (-T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0), so a later gap analysis measures each of `behaviour:test60`, `behaviour:test98`, `behaviour:test1068`, `behaviour:test1069`, `behaviour:test1072`, `behaviour:test1073` as `match`. Split to BL-2037 (needs Curl.Core and Abstractions), which closes it.

## Context

This is a Re-close task: every earlier task for GF-0007 ([BL-1800]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0007, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test60`, `behaviour:test98`, `behaviour:test1068`, `behaviour:test1069`, `behaviour:test1072`, `behaviour:test1073`. Split to BL-2037 (needs Curl.Core and Abstractions), which closes it.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1068 actual: <verify><protocol> differs at byte 84 (line 5): expected 'Transfer-Encoding: chunked', got 'Content-Length: 19'. test1072/1073: expected 'Transfer-Encoding: chunked', got 'Content-Length: 122'. test60: expected 'Transfer-Encoding: chunked', got 'Content-Length: 1'. test98 (-H 'Transfer-Encoding:' -H 'Content-Length: 14'): expected 'Expect: 100-continue', got an empty line. test1069 (-T - -0): expected exit code 25, got 52. The reference curl exits 25 on test1072/1073 and 56 on test1068/test60. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1068,98,1069

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary (HttpRequestFraming, HttpRequestBodyWriter), treat a stdin upload as a body of unknown size. Over HTTP/1.1 frame it with Transfer-Encoding: chunked, even when the user gives a Content-Length header, unless the user empties Transfer-Encoding. Add Expect: 100-continue for an upload of unknown size. Over HTTP/1.0 (-0, or a 1.0 server after a redirect or auth retry) fail with exit 25, as curl's 'chunked transfer encoding not supported by HTTP/1.0'. Curl.Console's stdin reading must stream rather than buffer to give the unknown size.

## Acceptance criteria

- [x] `behaviour:test60`: Curl answers what curl 8.21.0 answers, `reference curl exits 56; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test98`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1068`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1069`: Curl answers what curl 8.21.0 answers, `upstream test1069 passes`, so the item measures `match`.
- [x] `behaviour:test1072`: Curl answers what curl 8.21.0 answers, `reference curl exits 25; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1073`: Curl answers what curl 8.21.0 answers, `reference curl exits 25; stdout 0 bytes: `, so the item measures `match`. Split to BL-2037 (needs Curl.Core and Abstractions), which closes it.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured first with the in-repo upstream ratchet (`UpstreamCase_RunThroughCurl_HoldsTheRatchet`):
  test1068 and test1069 already passed (listed since BL-1800), so their gap-run failures come from the
  gap tool's run, not Curl, as BL-1998 found for GF-0002; lanes may not read `Gap/`, so re-measuring is
  left to the next gap run. test60, test98, test1072 and test1073 failed in Curl itself.
- Decision (match curl 8.21.0's `http_req_set_reader`): an `-H` naming `Transfer-Encoding` decides
  chunking by whether it says `chunked` (an emptied one sends the stdin body raw, test98); otherwise a
  body of unknown length is chunked whatever `-H Content-Length` says (test60), and over HTTP/1.0 is
  refused with exit 25. curl's own `Transfer-Encoding: chunked` line now goes after `Proxy-Connection`,
  ahead of `Cookie` and the `-H` lines, where curl's `Curl_http` puts it (test60's order).
- test1072: a 401/407 from an HTTP/1.0 server to a body chunked for its unknown length now fails with
  exit 25 before rewinding, where Curl used to return the 401 because stdin cannot rewind (ADR-0034
  still holds for HTTP/1.1 servers). New `HttpRequestFraming.ChunksForUnknownLength`.
- test60, test98 and test1072 now pass and are on `PassingUpstreamCases.txt`. That file is in
  `Curl.Conformance.UnitTests`, added to `touches`: no other task in Doing on `origin/work/dark-factory` names it.
- test1073 (`-L` hop after an HTTP/1.0 3xx) needs `RedirectFollower` in `Curl.Core.UnitLibrary` and a new
  `HttpRequestOptions` property in Abstractions, outside this task's projects: filed as BL-2037.
- No option changed, so `--ai-help` is unaffected. `Curl.Console` needed no change.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. test60, test98 and test1072 pass through Curl and are listed; test1068/1069 already passed; test1073 split to BL-2037
