---
id: BL-2006
title: Re-close GF-0013: --skip-existing skips the transfer silently; curl writes 'Note: skips transfer, <file> exists locally'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2038]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2006 — Re-close GF-0013: --skip-existing skips the transfer silently; curl writes 'Note: skips transfer, <file> exists locally'

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0013 (--skip-existing skips the transfer silently; curl writes 'Note: skips transfer, "<file>" exists locally'), so a later gap analysis measures each of `behaviour:test994`, `behaviour:test996`, `behaviour:test1491` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0013 ([BL-1806]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0013, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Medium. Introduced in: not stated upstream.
- Items: `behaviour:test994`, `behaviour:test996`, `behaviour:test1491`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test996 actual: <verify><stderr> differs at byte 0 (line 1): expected 'Note: skips transfer, "<LOGDIR>/there" exists locally', got the end. test994 (with globbing) and test1491 (file://) have the same shape. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 994,996,1491

Suggestion, copied from the finding:

In Curl.Console, where --skip-existing decides to skip a transfer, write curl 8.21.0's note to stderr: 'Note: skips transfer, "<output path>" exists locally', one per skipped URL. Write it even under -s, as upstream's <verify><stderr> shows.

## Acceptance criteria

- [ ] `behaviour:test994`: Curl answers what curl 8.21.0 answers, `upstream test994 passes`, so the item measures `match`.
- [ ] `behaviour:test996`: Curl answers what curl 8.21.0 answers, `upstream test996 passes`, so the item measures `match`.
- [ ] `behaviour:test1491`: Curl answers what curl 8.21.0 answers, `upstream test1491 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- Measured 2026-10-10 (lane 2): upstream tests 994, 996 and 1491 (curl-8_21_0 tag) expect the note
  because runtests.pl (runner.pm) adds `--trace-ascii $LOGDIR/trace<N> --trace-time` to every
  command, and curl 8.21.0's `notef` prints only with a trace type set. The built Curl.Console
  prints `Note: skips transfer, "log/there" exists locally` for test996's command line with those
  options, and both notes for test994's glob. No Curl.Console change is needed; the finding's
  suggestion (print without a trace option) would break drop-in compatibility (ADR-0447).
- The ratchet now runs those options (BL-2017), and the only difference left is the harness's: its
  absolute `%LOGDIR` (~100 characters) makes Curl wrap the note at 79 columns as curl does, where
  runtests.pl's `COLUMNS=79` with `log/` keeps it on one line. Filed as BL-2038 (touches
  Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests, which overlap BL-2037's
  `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, in Doing); this task depends on it.
  Once it is Done, this task only has to confirm the three cases pass and close.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Backlog. Waits on BL-2038: Curl already prints the note under runtests' trace options; the upstream harness's long absolute LOGDIR wraps it
- 2026-10-10: Backlog -> Doing.
