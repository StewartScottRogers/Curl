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
completed:
---
# BL-2641 — Run upstream cases 1506 and 1510: the runner applies the left-intact stripfile line

## Goal

Upstream cases 1506 and 1510 run through the runner instead of being skipped, and hold the ratchet once they pass.

## Context

Found by BL-2537, which ported lib1506 and lib1510 (`Curl.Conformance.LibTest.UnitLibrary/Lib1506.cs`, `Lib1510.cs`). Both cases are skipped before the program runs: `test1506 is skipped: the harness does not run the Perl $_ = '' if(($_ !~ /left intact/) and ($_ !~ /Closing connection/))` (`UpstreamCaseScreening.cs`). That `<stripfile>` line keeps only the stderr lines holding `left intact` or `Closing connection` and blanks the rest; teach the runner's stripfile translation this form (a line blanked unless it matches one of the patterns), with its own unit test, then run both cases. Their expected stderr is `* Connection #N to host serverN.example.com:<port> left intact` for N 0 to 3 (MAXCONNECTS 3), so they also check Curl's verbose connection lines; a difference there is filed as its own task.

## Acceptance criteria

- [ ] The runner applies the `left intact`/`Closing connection` stripfile line, with a unit test in `Curl.Conformance.UnitTests`.
- [ ] test1506 and test1510 run through the runner on Windows; each that passes is added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each that fails is filed with the board script with its differing lines.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-17: Created.
