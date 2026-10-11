---
id: BL-2009
title: Re-close GF-0016: -J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
lane: no
requirement: none
created: 2026-10-10
completed:
---
# BL-2009 — Re-close GF-0016: -J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0016 (-J with -L does not take the file name from the last Location, and -OJ --no-clobber --retry differs), so a later gap analysis measures each of `behaviour:test1642`, `behaviour:test1643`, `behaviour:test3036` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0016 ([BL-1809]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0016, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test1642`, `behaviour:test1643`, `behaviour:test3036`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1642 (-J -L -O, no Content-Disposition): <verify><file> <LOGDIR>/16420002 differs at byte 0: expected '12345', got the end (no such file). test1643 (two redirects) has the same shape. test3036 (--no-clobber --output-dir ... -OJ --retry 1 --retry-all-errors): stderr differs from the reference curl, which exits 23. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1642,1643,3036

Suggestion, copied from the finding:

In Curl.Console's -O/-J output naming: with -J and -L and no Content-Disposition, name the file after the last URL followed (the final Location's last path segment), as curl 8.21.0 does. With --no-clobber and --retry, fail on an existing file with exit 23 and the reference's message, as upstream test3036 expects.

## Acceptance criteria

- [x] `behaviour:test1642`: Curl answers what curl 8.21.0 answers, `upstream test1642 passes`, so the item measures `match`.
- [x] `behaviour:test1643`: Curl answers what curl 8.21.0 answers, `upstream test1643 passes`, so the item measures `match`.
- [ ] `behaviour:test3036`: Curl answers what curl 8.21.0 answers, `reference curl exits 23; stdout 0 bytes: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-10 (dark factory lane 1): both earlier fixes (BL-1809's 7af76995b, BL-1849's
  b727ac21d) were on `master` before the gap run of 2026-10-10 06:57, yet it still measures
  the gap. Replayed with `Record-CurlExchange.ps1` against curl 8.21.0 (Schannel) and this
  tree's Curl, `-J -L -O --output-dir <dir>`: a 301 with a relative `Location: /16420002`,
  a 301 with an absolute Location and a body, and two hops (`/dir/16430002` then
  `16430003?x=1`). Every case saves the same file (`16420002` or `16430003`, `12345`) with
  the same exit code; no difference reproduces. What the gap harness sends for test1642,
  test1643 and test3036, and how it runs Curl, is only in `Gap/Tools/Measure-UpstreamCases.cs`
  and the gap office's upstream cache, both of which the audit guard refuses to lanes (as
  for BL-1849). Added `lane: no`: an interactive session should rerun the finding's
  reproduction command, check that it measured a current Curl build, and fix what then
  differs - or record that the items now pass for the next gap run. No code changed.

- 2026-10-10 (interactive, gap PR #108 2fd5b394d): the gap tool passes test1642 and test1643 on the current build, and both measure match. test3036's gap comes from the reference cross-check (Measure-ReferenceCrossCheck.ps1), not the harness, and reproduces 3 times out of 3. Both binaries exit 23 with the same request bytes, and the first attempt reports 'write of 51 bytes' in both. On the retry, real curl ends 'curl: (23) client returned ERROR on write of 16 bytes' and Curl's Release build ends 'write of 128 bytes'. Left for a lane, in Curl.Console: report curl's byte count for the failed write on the retry of an -OJ --no-clobber --retry transfer (test3036). lane: no removed.

- 2026-10-10 (dark factory lane 1): the retry's 16 vs 128 bytes does not reproduce outside
  the cross-check. Replayed test3036's exchange with `Record-CurlExchange.ps1` (`-Connections 2`,
  the same reply both times, `http://127.0.0.1:<port>/3036 --no-clobber --output-dir present -OJ
  --retry 1 --retry-all-errors --no-progress-meter`, `present` a file) with upstream's LF-only
  head (`HTTP/1.1 200 OK`, `Content-Length: 6`, `Connection: close`, the `Content-Disposition`
  line, `Content-Type: text/html`, body `-foo-\n`). curl 8.21.0 (Schannel) and this tree's Debug
  build print byte-identical stderr: `write of 51 bytes` (the LF-only `Content-Disposition` line),
  the retry warning, then `write of 6 bytes` (the body), exit 23. With CRLF lines it is 52 then 6
  for both (BL-1849). The numbers in the finding fit a different exchange on the cross-check's second
  connection: 16 is the length of `HTTP/1.1 200 OK\n` and 128 is the whole LF-only head above. So
  curl failed its retry at the status line and Curl wrote the head as one block. That points at
  what `Measure-ReferenceCrossCheck.ps1` serves or runs for the second attempt (a different reply,
  `-i`, or an old Release build), which only the gap office's files show, and the audit guard
  refuses those to lanes. Added `lane: no` again. An interactive session should capture the
  cross-check's second request and reply (its request.bin and the bytes sent), replay them with
  Record-CurlExchange against both binaries, and fix Curl.Console if they then differ. No code
  changed.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Backlog. Interactive only: the gap harness and upstream cache for test1642/1643/3036 are refused to lanes, and no difference reproduces with Record-CurlExchange; added lane: no
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Backlog. Interactive only: test3036's retry difference (16 vs 128 bytes) appears only in the gap office's cross-check, which lanes may not read; with Record-CurlExchange, CRLF and LF-only replies, curl 8.21.0 and Curl print identical stderr; added lane: no
