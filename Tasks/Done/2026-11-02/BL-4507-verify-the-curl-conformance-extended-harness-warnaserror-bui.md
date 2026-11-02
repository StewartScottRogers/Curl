---
id: BL-4507
title: Verify the Curl.Conformance.Extended harness: warnaserror build, fast tests, coverage gates and harness ADR
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4505, BL-4506]
touches: [Curl.Conformance.Extended.UnitLibrary/Harness, Curl.Conformance.Extended.UnitTests/Harness, Documentation/Planning/Decisions]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4507 — Verify the Curl.Conformance.Extended harness: warnaserror build, fast tests, coverage gates and harness ADR

## Goal

The Extended conformance harness passes every gate: solution build, fast tests, 100% coverage of the UnitLibrary, and an ADR records its design.

## Context

Split from BL-4501 (see BL-4501 and BL-4462). Runs after BL-4505 and BL-4506. ADRs live in Documentation/Planning/Decisions; mark it "Decided by Claude under Stewart's delegation" and add its README row.

## Acceptance criteria

- [x] dotnet build Curl.slnx -warnaserror is clean and the fast tests are green.
- [x] Curl.Conformance.Extended.UnitLibrary is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary); missing cases are added as tests.
- [x] An ADR in Documentation/Planning/Decisions records the harness design (curl entry point as a delegate, route by port, recording at the server) and its README row exists.
- [x] No package added; every test is platform-neutral.

## Notes

- Split from BL-4501.
- 2026-11-02: `dotnet build Curl.slnx -warnaserror` clean (0 warnings); fast tests green across 115 test assemblies.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary: line 100%, branch 100%, 65 members, 0 failing, worst CRAP 8; no tests needed adding.
- ADR-1185 records the harness design (curl entry point as the `InProcessCurlRunner` delegate, routes by port with every host on 127.0.0.1, recording at the reference server, plaintext behind TLS).
- No package added (only project references). The harness tests use no file paths, OS error text or platform certificates (the TLS certificate is made in memory), so they are platform-neutral; CI on Linux and macOS is left to the shift's CI watch.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: a18d009a 5e07140c 8108353b feac4ffa.
- 2026-11-02: Doing -> Backlog. Returned from Doing at shift start: no lane held it and no work for it exists anywhere (no uncommitted work, no parked branch, no lane branch ahead of the shift branch).
- 2026-11-02: Backlog -> Doing. Criteria at claim: a18d009a 5e07140c 8108353b feac4ffa.
- 2026-11-02: Doing -> Done. Extended conformance harness passes warnaserror build, fast tests and 100% coverage gates; ADR-1185 records its design
