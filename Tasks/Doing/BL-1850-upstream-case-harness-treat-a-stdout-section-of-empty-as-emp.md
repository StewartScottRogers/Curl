---
id: BL-1850
title: Upstream-case harness: treat a stdout section of %EMPTY as empty output
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1850 — Upstream-case harness: treat a stdout section of %EMPTY as empty output

## Goal

The gap office's upstream-case measuring tool (the one GF-0019's "Reproduce" line runs) reads an upstream `<stdout>` section holding only `%EMPTY` as "expect no output", so upstream test2013 and test2014 measure `match`.

## Context

- Filed by a dark factory lane under BL-1812. The comparison lives in Curl.Conformance.UnitLibrary (UpstreamCaseRunner and its verify-section reading), not in Gap/, so a lane may do it; Gap/Tools/Measure-UpstreamCases.cs only calls it.
- In upstream's test format, `%EMPTY` in a section means the section is empty. GF-0019 reports test2013/test2014 as "expected '%EMPTY', got the end": the tool compares the literal text `%EMPTY` with Curl's (correctly) empty stdout.
- BL-1812 measured curl 8.21.0 and Curl on both commands on 2026-10-08: both write nothing to stdout, send the same three (test2013) or two (test2014) requests byte for byte, and write the same output files. Nothing in Curl needs to change.
- Any other `%`-variable upstream allows in a verify section may need the same treatment; check the tool's substitution list.

## Acceptance criteria

- [x] The measuring tool treats a verify section whose content is `%EMPTY` as empty.
- [x] Re-measuring upstream cases 2013 and 2014 gives `match`, closing GF-0019.

## Notes

- `UpstreamCaseVerification` now reads a verify part whose body is exactly the line `%EMPTY`
  as no bytes. It applies to every verify part read through the shared expected-body path
  (stdout, stderr, protocol, `<file>`), not stdout alone: upstream uses the marker to say
  "this part exists and is empty", and the harness's only other `%` handling (variables,
  character macros, instructions) runs at expansion and never touches it. No other
  `%`-variable in a verify section needs this treatment.
- Measured with the in-process ratchet: test2012, test2013 and test2014 now pass and are
  added to `PassingUpstreamCases.txt` (Conformance: 1176 passed, 0 failed). GF-0019 itself
  closes on the gap office's next run (lanes may not touch `Gap/`).
- New test `FindFirstDifference_StdoutOfEmptyMarker_ExpectsNoOutput` covers both branches of
  the new check; Measure-CodeQuality.ps1 was not run for a one-expression change.

## Log

- 2026-10-08: Created.
- 2026-10-09: Backlog -> Doing.
