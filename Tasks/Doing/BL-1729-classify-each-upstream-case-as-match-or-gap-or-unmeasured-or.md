---
id: BL-1729
title: Classify each upstream case as match or gap or unmeasured or excluded with Gap/Tools/ConvertTo-BehaviourMeasurement.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1728]
touches: [Gap/Tools/ConvertTo-BehaviourMeasurement.ps1, Gap/Tools/Fixtures/behaviour]
requirement: none
created: 2026-10-08
completed:
---
# BL-1729 — Classify each upstream case as match or gap or unmeasured or excluded with Gap/Tools/ConvertTo-BehaviourMeasurement.ps1

## Goal

`Gap/Tools/ConvertTo-BehaviourMeasurement.ps1` turns the raw case outcomes of
`Measure-UpstreamCases.cs` (BL-1728) into the `behaviour` area measurement. Every upstream
case gets one state and, where the state needs one, a reason. The cases Curl cannot be
measured on are then counted openly, never hidden.

## Context

This is ADR-0433 decision 2. A case is `match` when it passed and `gap` when it failed.
`unmeasured` and `excluded` each carry a reason from the closed vocabulary in
`Gap/Instructions/Gap-Format.md` (BL-1720). The score is X = match over
Y = match + gap + unmeasured.

**Inputs.** `-RawFile` (BL-1728's JSON), `-TestsData` (the release's `tests/data` folder,
for reading each case's tags), `-ReferenceFeatures` (the names on the matched reference
build's `curl -V` `Features:` and `Protocols:` lines, passed in by the orchestrator BL-1740;
empty means no reference), `-OutFile`, `-SelfTest`.

**Rules, in this order, for a case the harness skipped.** Read the case's `<client>`,
`<tool>`, `<server>`, `<features>` and `<keywords>` parts. The test file format is upstream's
`FILEFORMAT.md`. Look for it under the release's `docs/` (`docs/tests/FILEFORMAT.md` in
recent releases), which BL-1721 extracts. The harness's parser in
`Curl.Conformance.UnitLibrary/UpstreamTestCaseParser.cs` shows how those parts are read.

1. `<tool>` names a `lib…` program gives `excluded` with `libcurl-api`. Curl is the command
   line, with no libcurl C API.
2. `<tool>` names a `unit…` program gives `excluded` with `libcurl-unit-test`.
3. `<features>` needs `Debug`, `TrackMemory` or `unittest` gives `excluded` with
   `debug-build-only`. A release build of curl skips these too (ADR-0420 lists these
   libcurl build features).
4. `<features>` needs a name absent from `-ReferenceFeatures` (when it is given) gives
   `excluded` with `reference-lacks:<name>`. The matched build would skip the case too.
5. `<server>` names a server other than `http` (for example `ftp`, `imap`, `smtp`,
   `pop3`, `rtsp`, `tftp`, `socks4`, `dict`, `https`, `http/2`) gives `unmeasured` with
   `needs-server:<server>`.
6. The harness's reason says a variable is unknown gives `unmeasured` with
   `unknown-variable`.
7. Anything else gives `unmeasured` with `harness-unsupported`, and the harness's reason
   goes in `evidence`.

A passed case is `match`. A failed case is `gap`, with the harness's first difference as
`actual` and the case's keywords in `attributes.keywords`, so the analyst (BL-1739) can
group by cause. The measurement also carries a summary of counts per reason. That table is
what the scorecard's "Unmeasured by reason" section prints.

Key: `behaviour:test<N>`. Write the measurement in the `Gap-Format.md` area format, with
`reference` set to the reference's version line when `-ReferenceFeatures` was given.

**Self-test.** Fixtures under `Gap/Tools/Fixtures/behaviour/`: a raw file with one case for
each rule above, plus a pass and a failure, and the matching fake `tests/data` files.

## Acceptance criteria

- [ ] `Gap/Tools/ConvertTo-BehaviourMeasurement.ps1 -SelfTest` prints `PASS` lines and no `FAIL` under Windows PowerShell 5.1 and PowerShell 7. There is one check per rule 1 to 7, plus pass gives `match`, failure gives `gap` with its first difference, and a summary of counts per reason that adds up to the case count.
- [ ] A real run on the 8.21.0 raw output from BL-1728 writes a measurement whose `counts` add up to the number of `test*` files. Its per-reason table is recorded in this task's Notes.
- [ ] The header help documents every parameter and the rule order. The script is ASCII only.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
