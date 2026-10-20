---
id: BL-2075
title: Re-close GF-0007: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
lane: no
requirement: none
created: 2026-10-11
completed: 2026-10-19
---
# BL-2075 — Re-close GF-0007: -T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0007 (-T - (stdin) is read whole and sent with Content-Length, not chunked; no Expect: 100-continue, and no exit 25 under HTTP/1.0), so a later gap analysis measures each of `behaviour:test60`, `behaviour:test98`, `behaviour:test1068`, `behaviour:test1069`, `behaviour:test1072`, `behaviour:test1073` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0007 ([BL-1800, BL-2003]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

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

- [x] This task is obsolete, and closes without a change: GF-0007 is closed.

## Notes

- 2026-10-11, lane 2 (interactive only): the gap could not be reproduced from a lane. All six
  cases (60, 98, 1068, 1069, 1072, 1073) are listed in
  `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, which the in-repo upstream ratchet
  (`UpstreamCase_RunThroughCurl_HoldsTheRatchet`) holds. BL-2003 and BL-2037 (archived in
  bf68ab5ce, 2026-10-10 19:34) reached `master` before the 2026-10-11 gap run, and
  `Curl.Console/StandardInputUploadStream` already hides standard input's size, so `-T -` is
  chunked with `Expect: 100-continue` and refused with exit 25 under HTTP/1.0. The evidence
  (`Content-Length: 19` for test1068) says the gap tool's run of Curl sized standard input -
  most likely it runs another binary or build, or feeds `<stdin>` another way - which only
  `Gap/Tools/Measure-UpstreamCases.cs` shows, and lanes may not read `Gap/` (ADR-0433).
- Remove `lane: no` once an interactive session has run the finding's reproduction for 1068, 98
  and 1069 and copied under these Notes the exact Curl binary, command line and stdin the gap
  tool used, with Curl's actual request bytes and exit code, so a lane can fix the cause without
  reading `Gap/`.
- 2026-10-11: Product defects found by re-measuring with the fixed gap harness (PR #119) are filed as BL-2087 and BL-2088.

- 2026-10-19: Stewart had it closed as obsolete. Its criteria were replaced by the one above while it was in the Backlog, and the evidence was checked mechanically first.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. Interactive only: all six cases pass the in-repo upstream ratchet; the gap differs only under the gap tool, which lanes may not read (see Notes)
- 2026-10-11: Backlog -> Deferred. product fixes BL-2084..BL-2092 are done (BL-2087 blocked on the cost cap); this task closes when the next gap run re-measures its items
- 2026-10-19: Deferred -> Backlog. Stewart: close it as obsolete (GF-0007 is closed).
- 2026-10-19: Backlog -> Doing. Interactive session: closing it as obsolete. Criteria at claim: 5dfe5593.
- 2026-10-19: Doing -> Done. Obsolete: GF-0007 is closed.
