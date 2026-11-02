---
id: BL-4503
title: Finish the extended-protocol conformance harness: -m timeout on an injected TimeProvider, the conformance README and the ADR
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502]
touches: [Curl.Conformance.Extended.UnitLibrary/Harness/ExtendedCurlRun.cs, Curl.Conformance.Extended.UnitTests/Harness/ExtendedCurlRunTimeoutTests.cs, Documentation/Conformance/Extended/README.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-02
completed:
---
# BL-4503 — Finish the extended-protocol conformance harness: -m timeout on an injected TimeProvider, the conformance README and the ADR

## Goal

The extended-protocol conformance harness runs `-m` on an injected `TimeProvider`, and its README and ADR state the contract, so the 20 per-protocol conformance tasks can start.

## Context

Split from BL-4462 as its last part; read BL-4462 (in `Tasks/Deferred`) for the full plan. Wire the harness run's `TimeProvider` to the `InProcessCurl` overload BL-4500 adds. Write `Documentation/Conformance/Extended/README.md` (layout, run API, scripted server, ID scheme, matrix format, known-failures rule, extension isolation) and the ADR: the next free number, slug `extended-protocols-conform-to-their-own-specifications`, marked "Decided by Claude under Stewart's delegation": extended protocols are measured against their own specifications, not against curl; how the harness reaches the extensions within `VerifyExtensionIsolation` (the tests reference the hosts, the library never an extension); why reference servers do not reuse client codecs; the ID, matrix and known-failures rules.

## Acceptance criteria

- [ ] A test in `Curl.Conformance.Extended.UnitTests/Harness/` runs `-m` against a reference server that never answers, on an injected `TimeProvider`, and gets exit 28 with no real wait.
- [ ] `Documentation/Conformance/Extended/README.md` states the layout, ID scheme, matrix format and known-failures rule.
- [ ] The new ADR exists with the decisions above and "Decided by Claude under Stewart's delegation", and its line is in `Documentation/Planning/Decisions/README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; `Curl.Conformance.Extended.UnitLibrary` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral.

## Notes

## Log

- 2026-11-02: Created.
