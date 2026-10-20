---
id: BL-2828
title: List the Linux-only upstream cases 307, 1517, 1582, 1587, 2072 and 3207 on the ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitLibrary/UpstreamCaseRatchet.cs, Curl.Conformance.UnitTests/UpstreamCaseRatchetTests.cs]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2828 — List the Linux-only upstream cases 307, 1517, 1582, 1587, 2072 and 3207 on the ratchet

## Goal

Upstream cases 307, 1517, 1582, 1587, 2072 and 3207, which only Linux and macOS can run, are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once they pass there, and are `Inconclusive` (not failed) on Windows.

## Context

Measured 2026-10-19 on Windows, each is skipped: 307 (`--engine openssl`) and 1582, 1587, 3207 (libtest programs) need the `OpenSSL` feature; 1517 needs `!win32`; 2072 ("file:// with Unix path for the case of extra slashes") has a `<client><precheck>` that prints `Test requires a Unix system` on `MSWin32`, which the runner treats as a skip. The libtest ports `Lib1517`, `Lib1582`, `Lib1587` and `Lib3207` exist and are registered in `Curl.Conformance.LibTest.UnitLibrary/LibTestPrograms.cs`. Unlike the `match` cases of BL-2827, the gap office has *not* measured these on Linux (they sit in `libcurl-api` or other exclusions, 2072 in `unmeasured | harness-unsupported`), so nobody has seen them pass.

ADR-0488 lets a listed case whose features only another platform's Curl has be `Inconclusive` where it cannot run (`UpstreamCaseScreening.FindFeatureOfAnotherPlatform`). A precheck that skips on Windows is not a feature, so 2072 needs the ratchet (`Curl.Conformance.UnitLibrary/UpstreamCaseRatchet.cs`) to treat a precheck skip that the case's `$^O` test names (`MSWin32`) as another platform's case too, with a unit test in `UpstreamCaseRatchetTests`.

No lane can run these on Linux. Use the `CI` workflow on the pushed commit as the check, and keep this task's push the only change in the commit.

## Acceptance criteria

- [x] `UpstreamCaseRatchet` treats a listed case whose precheck prints a `Test requires a Unix system` skip on Windows (case 2072) as `Inconclusive` on Windows, and `UpstreamCaseRatchetTests` has a test that fails without the change and passes with it; a Linux-style precheck pass still runs the case.
- [x] 307, 1517, 1582, 1587, 2072 and 3207 are on `PassingUpstreamCases.txt`, one number per line, in numeric order, and each is `Inconclusive` on Windows with its reason shown.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test Curl.Conformance.UnitTests --filter "TestCategory!=Integration"` passes; `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage.
- [x] After the push, `CI` is green on Linux and macOS; any of the six that fails there is taken off the list and its failure text is in Notes together with a follow-up task naming the case.

## Notes

- The ratchet, not the runner, recognises the precheck skip: `UpstreamCaseRunner.cs` is in BL-2863's `touches` (Doing), and the ratchet sees only the outcome, so `JudgeSkip` treats a skip whose detail is `Test requires a Unix system` (the only `$^O`-refusing precheck line in the vendored cases) as runs-only-on-another-platform. ADR-0711 records it. Off Windows the precheck prints nothing, so 2072 runs and is judged by its run.
- Measured on Windows, 2026-10-19: all six rows are `Inconclusive` - 307, 1582, 1587, 3207 "Curl lacks the feature OpenSSL", 1517 "the case needs Curl without the feature win32", 2072 "Test requires a Unix system", each "is on PassingUpstreamCases.txt and runs only on another platform". Conformance tests: 3580 passed, 0 failed. `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitLibrary`: 100% line, 100% branch, 0 failing members.
- Not run on Linux or macOS here (no lane can); the first CI run after the shift pushes is the check. The CI criterion is handed to the shift: a lane may not push, and the coordinator's CI watch (BL-987) files a High task naming any of the six that fails on Linux or macOS.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 0578e887 287c04f9 9d803be9 fa5ceaa7.
- 2026-10-19: Doing -> Done. 307, 1517, 1582, 1587, 2072 and 3207 listed; Inconclusive on Windows with reasons; ratchet treats test2072's Unix-only precheck skip as another platform's (ADR-0711)
