---
id: BL-1806
title: Close GF-0013: --skip-existing skips the transfer silently; curl writes 'Note: skips transfer, <file> exists locally'
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1806 — Close GF-0013: --skip-existing skips the transfer silently; curl writes 'Note: skips transfer, <file> exists locally'

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0013 (--skip-existing skips the transfer silently; curl writes 'Note: skips transfer, "<file>" exists locally'), so a later gap analysis measures each of `behaviour:test994`, `behaviour:test996`, `behaviour:test1491` as `match`.

## Context

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

- [x] `behaviour:test994`: Curl answers what curl 8.21.0 answers, `upstream test994 passes`, so the item measures `match` (with the `--trace-ascii`/`--trace-time` upstream's harness adds; see Notes and ADR-0447).
- [x] `behaviour:test996`: Curl answers what curl 8.21.0 answers, `upstream test996 passes`, so the item measures `match` (with the `--trace-ascii`/`--trace-time` upstream's harness adds; see Notes and ADR-0447).
- [x] `behaviour:test1491`: Curl answers what curl 8.21.0 answers, `upstream test1491 passes`, so the item measures `match` (with the `--trace-ascii`/`--trace-time` upstream's harness adds; see Notes and ADR-0447).
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md). No option changed.

## Notes

- Measured 2026-10-08, curl 8.21.0 (mingw, Schannel): with the output file present,
  `curl -s URL -o there --skip-existing` prints nothing and exits 0; adding
  `--trace-ascii - --trace-time` prints `Note: skips transfer, "there" exists locally` CRLF to
  stderr and nothing to stdout. curl's `notef` prints only when a trace type is set, and
  upstream's runtests adds `--trace-ascii`/`--trace-time` to every curl command, which is why
  tests 994, 996 and 1491 expect the note.
- Curl already does this (BL-493); added
  `RunAsync_SkipExistingFilePresentSilentTraceAscii_PrintsTheNote` pinning the harness's command
  line. It passed with no product change. The finding's suggestion (always print, even plain)
  would break drop-in compatibility, so it was not taken: ADR-0447.
- What is left is in the gap office's runner (`Gap/Tools/Measure-UpstreamCases.cs`): it must pass
  Curl the trace options runtests passes curl. Lanes may not read or file tasks for `Gap/`
  (guard-audit-paths.ps1 refused the read), so that is for an interactive session; the items will
  measure `match` once it does.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Curl already prints the skip-existing note under the trace options upstream's harness adds, as curl 8.21.0 does; pinned by a test, ADR-0447
