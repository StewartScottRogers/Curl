---
id: BL-4503
title: Finish the extended-protocol conformance harness: -m timeout on an injected TimeProvider, the conformance README and the ADR
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502]
touches: [Curl.Conformance.Extended.UnitLibrary/Harness/ExtendedConformanceHarness.cs, Curl.Conformance.Extended.UnitLibrary/Harness/InProcessCurlRunner.cs, Curl.Conformance.Extended.UnitLibrary/CLAUDE.md, Curl.Conformance.Extended.UnitTests/Curl.Conformance.Extended.UnitTests.csproj, Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs, Documentation/Conformance/Extended/README.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4503 — Finish the extended-protocol conformance harness: -m timeout on an injected TimeProvider, the conformance README and the ADR

## Goal

The extended-protocol conformance harness runs `-m` on an injected `TimeProvider`, and its README and ADR state the contract, so the 20 per-protocol conformance tasks can start.

## Context

Split from BL-4462 as its last part; read BL-4462 (in `Tasks/Deferred`) for the full plan. Wire the harness run's `TimeProvider` to the `InProcessCurl` overload BL-4500 adds. Write `Documentation/Conformance/Extended/README.md` (layout, run API, scripted server, ID scheme, matrix format, known-failures rule, extension isolation) and the ADR: the next free number, slug `extended-protocols-conform-to-their-own-specifications`, marked "Decided by Claude under Stewart's delegation": extended protocols are measured against their own specifications, not against curl; how the harness reaches the extensions within `VerifyExtensionIsolation` (the tests reference the hosts, the library never an extension); why reference servers do not reuse client codecs; the ID, matrix and known-failures rules.

## Acceptance criteria

- [x] A test in `Curl.Conformance.Extended.UnitTests/Harness/` runs `-m` against a reference server that never answers, on an injected `TimeProvider`, and gets exit 28 with no real wait.
- [x] `Documentation/Conformance/Extended/README.md` states the layout, ID scheme, matrix format and known-failures rule.
- [x] The new ADR exists with the decisions above and "Decided by Claude under Stewart's delegation", and its line is in `Documentation/Planning/Decisions/README.md`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; `Curl.Conformance.Extended.UnitLibrary` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [x] No package added; every test is platform-neutral.

## Notes

- `ExtendedCurlRun.cs` named in `touches` never existed: the run lives in `ExtendedConformanceHarness.cs` and its curl
  entry point in `InProcessCurlRunner.cs`, so `touches` names those instead, with the library's `CLAUDE.md` and the test
  csproj (no other task in Doing held any of them).
- Decision: the `InProcessCurlRunner` delegate gains a `TimeProvider` parameter, so the tests' `InProcessCurl.RunAsync`
  method group binds to BL-4500's overload; `RunAsync` takes an optional `timeProvider`, `TimeProvider.System` when null
  (ADR-1187).
- The test csproj excludes `Documentation/Conformance/Extended/README.md` from the copied matrices: copied, it would read
  as a matrix named README with no rows and fail the matrix check.
- `ExtendedCurlRunTimeoutTests` runs `-m 100` against a Redis reference server that takes HELLO and never answers,
  advances a fake clock a second at a time, and gets exit 28 with no real wait (all 39 tests run in 1 s).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`: 100% line, 100% branch, 0 failing
  members, worst CRAP 10. `dotnet build Curl.slnx -warnaserror` clean; `Curl.Conformance.Extended.UnitTests` (the only
  user of the delegate) green. The test opens no socket or file and pins no OS text, so it is platform-neutral.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: ed383a02 27131998 bcfa9865 29c4c51e feac4ffa.
- 2026-11-02: Doing -> Done. Harness runs take an injected TimeProvider (-m exit 28 with no real wait), conformance README and ADR-1187 written
