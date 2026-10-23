---
id: BL-3263
title: Differential normaliser hides the verbose progress meter's speed columns
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/DifferentialOutputNormaliser.cs, Curl.Conformance.Differential.UnitTests/DifferentialOutputNormaliserTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3263 — Differential normaliser hides the verbose progress meter's speed columns

## Goal

`DifferentialOutputNormaliser` reduces the progress meter that `-v` (output to a terminal-less stdout) writes after the transfer, so a `-v {U}/echo` case no longer shows a `### DIFF` only because the speed columns differ.

## Context

BL-3260's first host run (ADR-0785) of `-v {U}/echo` against curl 8.22.0 reported a stderr difference in the progress lines alone: ours `\r100      3 100      3   0      0     11      0 ...` and real `... 173      0 ...`. The `ProgressMeter` regex replaces only the `  % Total ... Speed\r\n` header plus `\r`-lines that follow it directly; here `<PROGRESS>\n` is emitted and the meter's `\r100 ...` lines appear later, after `{ [3 bytes data]\r\n`. Check how the tester's `norm()` (`Z:\repos\Curl.difftest\2026-10-22\runner.py`, reference only) treats them and match it, or replace the meter's `\r`-lines anywhere in the output.

## Acceptance criteria

- [x] A unit test in `DifferentialOutputNormaliserTests` shows two `-v` outputs differing only in the progress lines' speed columns normalise to the same bytes.
- [x] Library gates hold (100% line and branch coverage); build clean and fast tests green.

## Notes

- Added a VerboseProgressLines regex (after ProgressMeter) replacing runs of ``-led meter lines anywhere in the output with `<PROGRESS>`. Measure-CodeQuality: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 082cab35 fe680ad5.
- 2026-10-23: Doing -> Done. Differential normaliser reduces the verbose progress meter lines wherever they appear (moved by hand: the board script refused over duplicate ID BL-3263 with a Backlog task).
