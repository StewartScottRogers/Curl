---
id: BL-5558
title: Fix AF-0887: Surviving mutant: TlsClientOptions' AllowBeast default can flip to true without a test noticing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsClientOptions.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5558 — Fix AF-0887: Surviving mutant: TlsClientOptions' AllowBeast default can flip to true without a test noticing

## Goal

The defect the audit office reported as AF-0887 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0887 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/TlsClientOptions.cs:232`

Location: `Curl.Networking.UnitLibrary/TlsClientOptions.cs:232`

Mutant bool AllowBeast = false, -> bool AllowBeast = true, survived all Curl.Networking.UnitTests (seed 446316). HandBuiltTlsProvider.InsertsEmptyFragment returns !options.AllowBeast, so every TlsClientOptions built without naming AllowBeast would turn off the BEAST empty-fragment mitigation that curl applies unless --ssl-allow-beast is given. That changes the TLS 1.0 CBC record bytes sent. No test checks the default.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/TlsClientOptions.cs line 232` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/TlsClientOptions.cs line 232`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by new test `TlsClientOptionsTests.Constructor_WithNoArguments_DoesNotAllowBeastSoTheEmptyFragmentMitigationStaysOn` (mutant applied by hand failed it; reverted, all 4383 Networking fast tests pass). Test-only change, no production code touched.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: bd9f742a 9742157d.
- 2026-11-19: Doing -> Done. Default test added; mutant killed
