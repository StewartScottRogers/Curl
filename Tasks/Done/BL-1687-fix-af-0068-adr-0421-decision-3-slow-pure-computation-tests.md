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
completed: 2026-10-07
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-07: The reproduction counts `TestCategory("LongRunning")` in `*.UnitTests`; only retagging the four Curl.Cryptography.UnitTests tests (BL-1604, in Doing on another lane, touching Curl.Cryptography.UnitTests) changes it. This task touches only Documentation, so it waits on BL-1604 and then verifies the reproduction gives 4.
- 2026-10-07 (lane 7): after BL-1604 the reproduction gives 3, not 0 or 4. BL-1604 retagged
  X25519, X448 and CAST-128 LongRunning with `[RunsOnlyWhenLongRunningTestsAreEnabled]`,
  and integration.yml runs them with `CURL_RUN_LONG_RUNNING_TESTS: '1'`; the Brainpool
  test measured under 3 s per row in Debug, so by decision 3's own drop-under-3-s rule it
  lost both attributes and joined the fast run. The code follows the ADR's rule; the ADR's
  list of four was what had gone stale. Fix: ADR-0421 decision 3 and its consequence line
  now name the three LongRunning tests, the attribute, and why Brainpool runs in the fast
  run. Decision (sensible default): the finding's expected count of 4 is superseded by 3,
  the number the corrected ADR names; the defect AF-0068 reports - ADR decision 3 not in
  the code - no longer reproduces. No `TestCategory("Integration")` remains in
  Curl.Cryptography.UnitTests.
- Verification: `dotnet build` 0 errors; fast tests 33 test assemblies passed, 0 failed
  (Curl.Cryptography.UnitTests 1451 passed, 3 skipped - the three LongRunning tests).

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Waits on BL-1604 (retags the four Curl.Cryptography.UnitTests slow tests LongRunning), which the reproduction needs
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. ADR-0421 decision 3 matches the code: three LongRunning Cryptography tests, Brainpool in the fast run
