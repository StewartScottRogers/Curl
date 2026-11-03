---
id: BL-4619
title: Conformance harness: let a run name host names that do not resolve, so curl exits 6
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4463]
touches: [Curl.Conformance.Extended.UnitLibrary/Harness/ExtendedConformanceHarness.cs, Curl.Conformance.Extended.UnitTests/Harness/ExtendedConformanceHarnessTests.cs, Documentation/Conformance/Extended/README.md]
requirement: none
created: 2026-11-02
completed:
---
# BL-4619 — Conformance harness: let a run name host names that do not resolve, so curl exits 6

## Goal

A conformance case can make a host name fail to resolve, so curl's exit 6 ("Could not resolve host") is testable for every extended scheme without a network.

## Context

Today `ExtendedConformanceHarness` resolves every host name to 127.0.0.1 (the comment near "Every host name is the reference servers' host"), so no case can reach exit 6. Add a way for a run to name host names that do not resolve (for example an optional set of unresolvable names on a `RunAsync` overload, or a reserved name such as `*.invalid` per RFC 6761), decided in an ADR marked "Decided by Claude under Stewart's delegation", and document it in the README's "A run" section. A port with no route already refuses (exit 7) through `ReferenceTcpDialer`.

Split from BL-4496 (the transfer surface for every extended protocol), which holds the whole plan; read its Context. Part of the extended-protocol conformance plan Stewart approved on 2026-11-02: follow `Documentation/Conformance/Extended/README.md`.

## Acceptance criteria

- [ ] A harness test runs a command line whose host the run names as unresolvable and gets exit 6 and curl's "Could not resolve host: <name>" error on stderr, while a host name not so named still resolves to the reference servers.
- [ ] `Documentation/Conformance/Extended/README.md` says how a case makes a host unresolvable, and the ADR records the choice.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`, opens a real socket or starts a real process.
- [ ] `Curl.Conformance.Extended.UnitLibrary` stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), if this task changes it.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 62c564ec 8e65dedd be674468 9438adeb ef6c9026.
