---
id: BL-1368
title: Run the quality auditor's scripted test checks over every test project
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Instructions/Quality.md]
lane: no
requirement: none
created: 2026-10-03
completed:
---
# BL-1368 — Run the quality auditor's scripted test checks over every test project

## Goal

The quality auditor's scripted checks cover every *.UnitTests project, so a planted weak test is in scope wherever the seeder puts it.

## Context

- Diagnosed 2026-10-03 across the audits of 2026-10-02 14:00 (run Z:\repos\Curl.audit\20261002-140002, planted commit 5232c4f8) and 2026-10-03 06:23 (run 20261003-062343, planted commit 5089bd0d): quality, truthfulness, process and conformance caught 0 of their planted defects. No auditor ran out of budget. Audit paths: interactive only, done on the `audit` branch and merged by pull request once CI is green.
- The catalogue lets the seeder plant in any *.UnitTests project, but the method read only the twins of the three libraries it mutated (Networking, Cryptography, Cli), missing PD-001 (Curl.Output.UnitTests/DiagnosticLogWriterTests.cs:25) and PD-002 (Curl.Core.UnitTests/ProtocolDispatcherTests.cs:135; then Curl.Console.UnitTests/CurlCommandRunnerEtagTests.cs:210). It also treated an IsNotNull as a sufficient assertion.

## Acceptance criteria

- [ ] Quality.md steps 1 and 2 run, over every *.UnitTests project including Curl.Console.UnitTests, scans for: tests whose only assertion is IsNotNull or IsTrue(length > 0); tests named *_ExitsWith<N> whose body never mentions N or its CurlExitCode; tests named *_Throws<X> whose body never mentions X. Then it reads 50 or more tests in the mutated twins.
- [ ] The scans' PowerShell commands are written out in Quality.md.
- [ ] A quality-only rerun on the run 1 and run 2 planted trees catches PD-001 and both PD-002s (Notes record it).
- [ ] `dotnet build` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-03: Created.
