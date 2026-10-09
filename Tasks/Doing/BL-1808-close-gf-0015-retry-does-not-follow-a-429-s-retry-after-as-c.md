---
id: BL-1808
title: Close GF-0015: --retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1842, BL-1843, BL-1844]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1808 — Close GF-0015: --retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning)

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0015 (--retry does not follow a 429's Retry-After as curl does (resend, --fail, --retry-max-time warning)), so a later gap analysis measures each of `behaviour:test366`, `behaviour:test1633`, `behaviour:test1634` as `match`.

## Context

- Finding: GF-0015, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test366`, `behaviour:test1633`, `behaviour:test1634`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test1633 (-d moo --retry 1 -L): the --output file against <reply><data> differs at byte 177 (line 12): expected 'HTTP/1.1 301 OK', got the end. test1634 (--retry 1 --fail): expected 'HTTP/1.1 429 too many requests swsbounce', got 'HTTP/1.1 200 OK'. test366 (--retry 2 --retry-max-time 10 with a too-long Retry-After): stderr differs from the reference curl, which exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 366,1633,1634

Suggestion, copied from the finding:

In Curl.Console's retry loop: on a 429 with Retry-After, keep the 429's output when the retry is made (test1634 under --fail). Resend the POST and then follow -L (test1633). When Retry-After exceeds --retry-max-time, give up the retry without a message, matching the reference curl's stderr for test366.

## Acceptance criteria

- [ ] `behaviour:test366`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1633`: Curl answers what curl 8.21.0 answers, `upstream test1633 passes`, so the item measures `match`.
- [ ] `behaviour:test1634`: Curl answers what curl 8.21.0 answers, `upstream test1634 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-08 (lane 1): Split into BL-1842 (test1633), BL-1843 (test1634) and BL-1844
  (test366), one per item, because the run's cost cap left no room to measure real curl and
  fix three behaviours in one run. This task now only checks that all three are Done; tick
  its boxes from theirs.
- The upstream cases cannot be read from the gap office's cache in a lane (the audit guard
  refuses any path under `.../Curl/gap/`); read them from
  `https://raw.githubusercontent.com/curl/curl/curl-8_21_0/tests/data/test<N>` instead.
- `touches` now adds Curl.Core.UnitLibrary and Curl.Core.UnitTests: the retry decision and
  the Retry-After handling live in `Curl.Core.UnitLibrary/TransferRetrier.cs`; no task in
  Doing named either (checked against origin/work/dark-factory).
- What the three cases expect, from curl 8.21.0's tests/data: test1633 resends the `-d moo`
  POST from the first URL after the redirect target's 429 and follows the 301 again (four
  requests); test1634 retries a 429 under `--fail` and its expected output keeps the 429's
  head ahead of the 200's; test366 makes one request only (Retry-After 200 > --retry-max-time
  10) and reference curl exits 0.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Backlog. Split into BL-1842, BL-1843 and BL-1844, one per gap item; waits on them
- 2026-10-08: Backlog -> Doing.
