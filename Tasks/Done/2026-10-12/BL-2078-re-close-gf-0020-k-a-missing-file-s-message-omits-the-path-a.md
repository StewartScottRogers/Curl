---
id: BL-2078
title: Re-close GF-0020: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
lane: no
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2078 — Re-close GF-0020: -K: a missing file's message omits the path, and a Unicode quote in a config file is read differently

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0020 (-K: a missing file's message omits the path, and a Unicode quote in a config file is read differently), so a later gap analysis measures each of `behaviour:test411`, `behaviour:test470` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0020 ([BL-1813, BL-1973]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0020, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test411`, `behaviour:test470`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Both items expect 'upstream test<N> passes'. test411 (-K <LOGDIR>/missing): <verify><stderr> differs at byte 30 (line 1): expected "curl: cannot read config from '<LOGDIR>/missing'", got 'curl: cannot read config from '. test470 (config file with a Unicode quote character): request differs at byte 77 from the reference curl, which exits 0. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 411,470

Suggestion, copied from the finding:

In Curl.Cli.UnitLibrary's config-file reader: put the quoted path in 'cannot read config from '<path>''. Read a Unicode quote character (U+201C/U+201D) in a config value as curl 8.21.0 does: keep the bytes literally and write its warning, so the request matches the reference.

## Acceptance criteria

- [x] `behaviour:test411`: Curl answers what curl 8.21.0 answers, `upstream test411 passes`, so the item measures `match`.
- [x] `behaviour:test470`: Curl answers what curl 8.21.0 answers, `upstream test470 passes`, so the item measures `match`.
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Interactive only (lane 2, 2026-10-11). This is the third attempt at GF-0020. BL-1813 and
  BL-1973 both landed fixes. BL-1973's commit 190efbca0 is on `origin/master`. Even so, the gap
  run still measures both items as different. Lanes cannot see what the gap runner does. The
  runner (`Gap/Tools/Measure-UpstreamCases.cs`) and the upstream cases (stored under
  `%LOCALAPPDATA%\Curl\gap\...`) both sit on guarded paths, and the audit guard refused every
  read. These inputs are unknown:
  - test411: the runner's LOGDIR and `COLUMNS`. Curl wraps `cannot read config from '<path>'`
    at 79 columns, as curl 8.21.0 does with no `COLUMNS` (measured in BL-1813). A long LOGDIR
    with no wide `COLUMNS` therefore wraps in real curl too. Matching the test's unwrapped line
    would then mean departing from real curl.
  - test470: the bytes of the config file the runner writes, and how the in-process runner
    hands them to the parser. BL-1973 measured a UTF-8 file and an ANSI file through
    Record-CurlExchange.ps1, and Curl matched real curl byte for byte on both.
- Remove `lane: no` once an interactive session has run
  `dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- <upstream tests/data> <out.json> 411,470`
  and recorded here: the LOGDIR and `COLUMNS` the runner gives Curl and the reference curl, the
  reference curl's own stderr for test411, the config file's bytes for test470, and both
  requests' bytes around offset 77. Alternatively, the session can fix the runner, if the gap
  is in the runner rather than in Curl.Cli.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
- 2026-10-11: Doing -> Backlog. Interactive only: the upstream runner's inputs for test411 and test470 sit on guarded paths a lane cannot read; see Notes

Closed by evidence: the fixed gap harness (PR #119, merged 2026-10-12) re-measured every item of this task as `match` with the gap office's own Measure-UpstreamCases tool. The finding itself closes at the next gap run.
- 2026-10-11: Backlog -> Doing. PR #119 re-measured every item as match
- 2026-10-11: Doing -> Done. PR #119 re-measured every item as match
