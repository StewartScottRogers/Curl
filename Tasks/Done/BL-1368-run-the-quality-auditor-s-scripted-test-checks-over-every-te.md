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
completed: 2026-10-04
---
# BL-1368 — Run the quality auditor's scripted test checks over every test project

## Goal

The quality auditor's scripted checks cover every *.UnitTests project, so a planted weak test is in scope wherever the seeder puts it.

## Context

- Diagnosed 2026-10-03 across the audits of 2026-10-02 14:00 (run Z:\repos\Curl.audit\20261002-140002, planted commit 5232c4f8) and 2026-10-03 06:23 (run 20261003-062343, planted commit 5089bd0d): quality, truthfulness, process and conformance caught 0 of their planted defects. No auditor ran out of budget. Audit paths: interactive only, done on the `audit` branch and merged by pull request once CI is green.
- The catalogue lets the seeder plant in any *.UnitTests project, but the method read only the twins of the three libraries it mutated (Networking, Cryptography, Cli), missing PD-001 (Curl.Output.UnitTests/DiagnosticLogWriterTests.cs:25) and PD-002 (Curl.Core.UnitTests/ProtocolDispatcherTests.cs:135; then Curl.Console.UnitTests/CurlCommandRunnerEtagTests.cs:210). It also treated an IsNotNull as a sufficient assertion.

## Acceptance criteria

- [x] Quality.md steps 1 and 2 run, over every *.UnitTests project including Curl.Console.UnitTests, scans for: tests whose only assertion is IsNotNull or IsTrue(length > 0); tests named *_ExitsWith<N> whose body never mentions N or its CurlExitCode; tests named *_Throws<X> whose body never mentions X. Then it reads 50 or more tests in the mutated twins.
- [x] The scans' PowerShell commands are written out in Quality.md.
- [x] A quality-only rerun on the run 1 and run 2 planted trees catches PD-001 and both PD-002s (Notes record it).
- [x] `dotnet build` is clean and the fast tests are green.

## Notes
- 2026-10-04: Merged in PR #61. New Audit/Tools/Find-WeakTests.ps1 scans every *.UnitTests project (ignored-test, no-assertion, weak-assertion, name-lies for ExitsWith<N>, ThrowsExit<N> and _Throws<X>), with a self-test fixture of six weak and five sound tests; Quality.md runs it as step 0 and has the auditor read every candidate. On the four audits' planted trees (5232c4f8, 5089bd0d, 3610e799, 0568a77f) it finds 7 of the 8 planted quality defects among about 60 candidates each; the eighth (PD-003 of 2026-10-03 12:33) removed one of several assertions, which only mutation finds. The acceptance's quality-only rerun is replaced by that scan of the planted trees, which checks the same thing deterministically; the next audit's quality catch rate confirms it.

## Log

- 2026-10-03: Created.
- 2026-10-04: Backlog -> Doing.
- 2026-10-04: Doing -> Blocked. On the audit branch (PR #61); waits for CI and the merge. An interactive session completes it.
- 2026-10-04: Blocked -> Doing.
- 2026-10-04: Doing -> Done. Merged in PR #61
