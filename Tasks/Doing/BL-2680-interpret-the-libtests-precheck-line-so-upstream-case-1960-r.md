---
id: BL-2680
title: Interpret the %LIBTESTS precheck line so upstream case 1960 runs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2680 — Interpret the %LIBTESTS precheck line so upstream case 1960 runs

## Goal

Upstream case 1960 (`application hands over already connected socket`, run by lib1960) runs (not skipped) through the case runner and, if it passes on Windows, holds the ratchet.

## Context

Found by BL-2547, which ported lib1960 (`Curl.Conformance.LibTest.UnitLibrary/Lib1960.cs`; its URL `check` answers `CURLE_OK` with no output, upstream's "no output makes it not skipped"). The case is skipped: `test1960 is skipped: the harness does not interpret the <client><precheck> line ./libtest/libtests lib1960 check`. Teach the runner in `Curl.Conformance.UnitLibrary` to run a `%LIBTESTS libNNN <args>` precheck through `LibTestPrograms.Ported` as it runs a case's `<tool>`, skipping the case when the program prints anything or exits non-zero (runtests.pl's rule), with a unit test in `Curl.Conformance.UnitTests`. Then run case 1960 with `CURL_UPSTREAM_CASE=1960`; if it fails, file its differing lines as a task and leave it off.

## Acceptance criteria

- [ ] A unit test in `Curl.Conformance.UnitTests` shows a `%LIBTESTS` precheck run through a ported program, a silent zero exit letting the case run and output or a non-zero exit skipping it.
- [ ] Upstream case 1960 is run, not skipped, and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` if it passes on Windows, or filed as a task with its differing lines if it does not.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened, and every case on the ratchet still passes.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: aea2b640 7875a3fd 4cf19161.
