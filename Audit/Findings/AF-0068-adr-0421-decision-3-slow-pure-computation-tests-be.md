---
id: AF-0068
title: ADR-0421 decision 3 (slow pure-computation tests become LongRunning with an env-var skip) is not in the code
auditor: truthfulness
severity: Medium
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-0421-integration-tests-live-only-in-integrationtests-projects.md:LongRunning:stale-adr
reproduction: none
task: BL-1687
tasks: BL-1687
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0068 - ADR-0421 decision 3 (slow pure-computation tests become LongRunning with an env-var skip) is not in the code

## Summary

Medium finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0421-integration-tests-live-only-in-integrationtests-projects.md`: ADR-0421 decision 3 (slow pure-computation tests become LongRunning with an env-var skip) is not in the code. Reported by an auditor flagged unreliable in 2026-10-07_1336.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0421-integration-tests-live-only-in-integrationtests-projects.md`

ADR-0421 (Status: Accepted) decision 3: four Curl.Cryptography.UnitTests tests (X25519Tests and X448Tests ...Rfc7748Section52MillionIterations_GivesTheExpectedK, Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB, BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP384r1AndP512r1Vector_GivesItsExpectedResult) lose the Integration tag and carry [TestCategory("LongRunning")] plus a ConditionBaseAttribute that skips unless CURL_RUN_LONG_RUNNING_TESTS=1, and integration.yml runs them. In the code, all four still carry [TestCategory("Integration")] (X25519Tests.cs:76, X448Tests.cs:76, Cast128Tests.cs:73, BrainpoolEcdsaTests.cs:71). git grep finds no 'LongRunning' category and no 'CURL_RUN_LONG_RUNNING_TESTS' in any .cs, .yml or .props file. The rule part (no Integration test in a *.UnitTests project) is not met either. Phase 2: BL-1604 'Retag Curl.Cryptography.UnitTests four slow known-answer tests' is in Backlog.

## Reproduction

Run from the repository root:

```powershell
(Get-ChildItem -Path *.UnitTests -Recurse -Filter *.cs | Select-String -SimpleMatch 'TestCategory("LongRunning")').Count
```

- Expected: 4 (the four Cryptography tests ADR-0421 names)
- Actual: 0

## Re-audits

- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | The count of TestCategory("LongRunning") in *.UnitTests is 3: Cast128Tests.cs:73, X25519Tests.cs:76 and X448Tests.cs:76, each also carrying [RunsOnlyWhenLongRunningTestsAreEnabled], which skips unless CURL_RUN_LONG_RUNNING_TESTS is 1. integration.yml runs them with the variable set. ADR-0421 decision 3 is in the code.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
