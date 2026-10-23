---
id: BL-3218
title: Decide and scaffold the differential conformance runner: ADR, projects, case model, report writer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary, Curl.Conformance.Differential.UnitTests, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-22
completed:
---
# BL-3218 — Decide and scaffold the differential conformance runner: ADR, projects, case model, report writer

## Goal

An ADR decides how the differential conformance runner works and where it runs, and `Curl.Conformance.Differential.UnitLibrary` with its `.UnitTests` holds the runner's core: the case model, a JSON case-file reader, a shell-style argument splitter, the output normaliser with the named reference-build exclusions, the comparer and a report writer in the tester's `### DIFF` shape, all over injected seams (no process, socket or file system).

## Context

The tester's harness and data are in `Z:\repos\Curl.difftest\2026-10-22\` (outside the repo; its Python scripts are a reference only - never commit or run Python). Parent task: BL-3217 (Stewart's 2026-10-22 prioritised findings list, "CI differential harness"); the runner's ADR comes from BL-3218. Read `runner.py` (case shape, `norm()`, report format) and `report.txt` (the `### DIFF name   args=...` / `exit: ours=X real=Y` / `stdout:` and `stderr:` blocks with `ours=b...` and `real=b...` lines, `### SLOW`). Use `/new-project`; the solution folder is `/Conformance/Differential/`. The existing conformance harness (`Curl.Conformance.UnitLibrary`) runs in process; this runner instead runs two real binaries, so the ADR must say which project hosts the real-process and socket parts (suggested default: a `Curl.Conformance.Differential.IntegrationTests` project whose tests are skipped unless an environment variable names the reference curl 8.22.0, so the UnitLibrary stays at 100% coverage) and whether it runs in CI (a Windows job that downloads curl 8.22.0) or on demand, and how it relates to the gap office's behaviour measurement (ADR-0433). Named exclusions to carry: ANSI arguments, 32-bit time_t, the debug build's `sendrecv_dl()` and `Curl_sendrecv()` lines, no HTTP/2, broken IDN (see `lib_summary.txt`). Case files are JSON read with `JsonDocument` (AOT: no reflection serialiser).

## Acceptance criteria

- [ ] An ADR marked "Decided by Claude under Stewart's delegation" exists and is in the Decisions README, naming where the runner runs, its host project and its named exclusions.
- [ ] Curl.Conformance.Differential.UnitLibrary and .UnitTests exist in Curl.slnx under /Conformance/Differential/.
- [ ] Unit tests show a case file read, a command line split, both outputs normalised (exclusions applied), and a difference written in the tester's ### DIFF shape byte for byte against an entry taken from report.txt.
- [ ] Projects build clean with -warnaserror and their fast tests pass on Windows; every test is platform-neutral (Linux and macOS CI).
- [ ] Measure-CodeQuality.ps1 reports 0 failing members for Curl.Conformance.Differential.UnitLibrary.

## Notes

- Split out of BL-3217 by its lane-3 run on 2026-10-22.

## Log

- 2026-10-22: Created.
