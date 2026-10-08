---
id: BL-1687
title: Fix AF-0068: ADR-0421 decision 3 (slow pure-computation tests become LongRunning with an env-var skip) is not in the code
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1604]
touches: [Documentation]
requirement: none
created: 2026-10-08
completed:
---
# BL-1687 — Fix AF-0068: ADR-0421 decision 3 (slow pure-computation tests become LongRunning with an env-var skip) is not in the code

## Goal

The defect the audit office reported as AF-0068 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0068 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0068-adr-0421-decision-3-slow-pure-computation-tests-be.md`.

Location: `Documentation/Planning/Decisions/ADR-0421-integration-tests-live-only-in-integrationtests-projects.md`

Location: `Documentation/Planning/Decisions/ADR-0421-integration-tests-live-only-in-integrationtests-projects.md`

ADR-0421 (Status: Accepted) decision 3: four Curl.Cryptography.UnitTests tests (X25519Tests and X448Tests ...Rfc7748Section52MillionIterations_GivesTheExpectedK, Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB, BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP384r1AndP512r1Vector_GivesItsExpectedResult) lose the Integration tag and carry [TestCategory("LongRunning")] plus a ConditionBaseAttribute that skips unless CURL_RUN_LONG_RUNNING_TESTS=1, and integration.yml runs them. In the code, all four still carry [TestCategory("Integration")] (X25519Tests.cs:76, X448Tests.cs:76, Cast128Tests.cs:73, BrainpoolEcdsaTests.cs:71). git grep finds no 'LongRunning' category and no 'CURL_RUN_LONG_RUNNING_TESTS' in any .cs, .yml or .props file. The rule part (no Integration test in a *.UnitTests project) is not met either. Phase 2: BL-1604 'Retag Curl.Cryptography.UnitTests four slow known-answer tests' is in Backlog.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Get-ChildItem -Path *.UnitTests -Recurse -Filter *.cs | Select-String -SimpleMatch 'TestCategory("LongRunning")').Count
```

- Expected: 4 (the four Cryptography tests ADR-0421 names)
- Actual: 0

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-07: The reproduction counts `TestCategory("LongRunning")` in `*.UnitTests`; only retagging the four Curl.Cryptography.UnitTests tests (BL-1604, in Doing on another lane, touching Curl.Cryptography.UnitTests) changes it. This task touches only Documentation, so it waits on BL-1604 and then verifies the reproduction gives 4.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Waits on BL-1604 (retags the four Curl.Cryptography.UnitTests slow tests LongRunning), which the reproduction needs
- 2026-10-07: Backlog -> Doing.
