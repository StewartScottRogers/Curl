---
id: BL-2641
title: Run upstream cases 1506 and 1510: the runner applies the left-intact stripfile line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-18
---
# BL-2641 — Run upstream cases 1506 and 1510: the runner applies the left-intact stripfile line

## Goal

Upstream cases 1506 and 1510 run through the runner instead of being skipped, and hold the ratchet once they pass.

## Context

Found by BL-2537, which ported lib1506 and lib1510 (`Curl.Conformance.LibTest.UnitLibrary/Lib1506.cs`, `Lib1510.cs`). Both cases are skipped before the program runs: `test1506 is skipped: the harness does not run the Perl $_ = '' if(($_ !~ /left intact/) and ($_ !~ /Closing connection/))` (`UpstreamCaseScreening.cs`). That `<stripfile>` line keeps only the stderr lines holding `left intact` or `Closing connection` and blanks the rest; teach the runner's stripfile translation this form (a line blanked unless it matches one of the patterns), with its own unit test, then run both cases. Their expected stderr is `* Connection #N to host serverN.example.com:<port> left intact` for N 0 to 3 (MAXCONNECTS 3), so they also check Curl's verbose connection lines; a difference there is filed as its own task.

## Acceptance criteria

- [x] The runner applies the `left intact`/`Closing connection` stripfile line, with a unit test in `Curl.Conformance.UnitTests`.
- [x] test1506 and test1510 run through the runner on Windows; each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each that fails is filed with the board script with its differing lines.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-17 (BL-2538, lane 1): test1512 (lib1512, ported by BL-2538) is skipped for the same `<stripfile>` line; once the runner applies it, run test1512 too and hold it on the ratchet if it passes.
- 2026-10-18 (lane 1): New `UpstreamPerlConditionalEmptying` (an `IUpstreamPerlLineEdit`) reads `$_ = '' if((T) and (T) ...)` with each term `$_ !~ /P/` or `$_ =~ /P/` and empties a line every term holds for; `IUpstreamPerlLineEdit.Parse` tries it after the substitution and field sort forms. Scope choice: only `and`-joined terms (the form 1506, 1510, 1512, 1542, 2402, 2404, 2502 use); test96's `||` form and test1400's `$_ = '' if /P/` stay unread, as they were. Tests in `UpstreamPerlConditionalEmptyingTests`.
- test1512 passes and is on `PassingUpstreamCases.txt` (it verifies only `<protocol>`, platform-neutral). test1506 and test1510 run and fail identically: `stderr1506 differs at byte 0 (line 1): expected "* Connection #0 to host server1.example.com:8990 left intact\r\n", got the end` - the `Libcurl` engine writes no `left intact` lines. Filed as BL-2726.
- Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 0fbc3dad 3c9a60b7 3d836d5e.
- 2026-10-18: Doing -> Done. The runner applies the left-intact stripfile line; 1506 and 1510 run (fail, BL-2726), 1512 passes and holds the ratchet
