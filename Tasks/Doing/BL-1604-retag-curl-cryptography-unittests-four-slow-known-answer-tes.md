---
id: BL-1604
title: Retag Curl.Cryptography.UnitTests' four slow known-answer tests from Integration to LongRunning and run them from the Integration workflow
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1525, BL-1598, BL-1603]
touches: [Curl.Cryptography.UnitTests, Directory.Build.props, .github/workflows/integration.yml, .github/integration]
requirement: none
created: 2026-10-07
completed:
---
# BL-1604 — Retag Curl.Cryptography.UnitTests' four slow known-answer tests from Integration to LongRunning and run them from the Integration workflow

## Goal

`Curl.Cryptography.UnitTests` holds no `[TestCategory("Integration")]` test: its four slow, CPU-only known-answer tests carry `[TestCategory("LongRunning")]` and a condition that skips them unless `CURL_RUN_LONG_RUNNING_TESTS=1`, the Integration tests workflow still runs them on all three platforms, and `Directory.Build.props`' allow-list for them is empty and gone.

## Context

- Rule (Stewart, 2026-10-07): an integration test touches something real outside the process and lives only in a `Curl.<Area>.IntegrationTests` project; no `*.UnitTests` project contains one.
- These four tests touch nothing outside the process; they are tagged Integration only because they are slow, which ADR-0118's "Tests" section asked for:
  - `X25519Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`
  - `X448Tests.TryComputeSharedSecret_Rfc7748Section52MillionIterations_GivesTheExpectedK`
  - `Cast128Tests.EncryptBlock_Rfc2144AppendixB2FullMaintenanceTest_GivesThePublishedAAndB`
  - `BrainpoolEcdsaTests.VerifyHash_EveryWycheproofP384r1AndP512r1Vector_GivesItsExpectedResult` (two data rows)
- Decision (made while planning on 2026-10-07 and recorded in BL-1598's ADR as its decision 3; read that ADR first and follow it if it differs from this summary): they stay in `Curl.Cryptography.UnitTests` - there is no `Curl.Cryptography.IntegrationTests`, because pure computation is not an integration test. They cannot join the fast run as they are: BL-1525's partial speed-up measured X25519's million iterations at 5 min 40 s, X448's at 14 min 51 s and CAST-128 B.2 at 32.9 s on 2026-10-07, and even an ideal constant-time X25519 is tens of seconds for a million operations, far over `TestDiagnostics`' 3-second `SLOW:` budget (ADR-0417). So each carries `[TestCategory("LongRunning")]` plus a condition attribute that skips it unless the environment variable `CURL_RUN_LONG_RUNNING_TESTS` is `1`. The fast command, `dotnet test --filter "TestCategory!=Integration"`, stays exactly as it is and reports them as skipped.
- Why this waits on BL-1525: BL-1525 ("Make X25519, X448 and CAST-128 fast enough ...") changes the same project, its criteria run these tests with `--filter "TestCategory=Integration"`, and part of its work sits in a lane's stash. After it lands, re-time each of the four in a Debug build: any that now finishes under 3 s (the Brainpool row may, if the separate Brainpool `VerifyHash` speed-up task has landed) loses both attributes and joins the fast run instead; Notes record the timings and which went where.
- The condition attribute: MSTest 4.4.1 (`Directory.Packages.props`) has a public abstract `ConditionBaseAttribute` with a `ConditionMode` constructor and `IsConditionMet`, `IgnoreMessage` and `GroupName` members, which `OSCondition` derives from. Write one in `Curl.Cryptography.UnitTests` with a name that says what it does (e.g. `RunsOnlyWhenLongRunningTestsAreEnabledAttribute`), reading the variable through `Environment.GetEnvironmentVariable`. Its `IgnoreMessage` says how to run the test (`set CURL_RUN_LONG_RUNNING_TESTS=1` and `--filter "TestCategory=LongRunning"`). Keep the comments above each test that cite the RFC and the iteration count; do not trim any iteration the RFC specifies.
- `.github/workflows/integration.yml` (a workflow of its own, not the `ci.yml` guard file): add a step after "Integration tests" that runs `dotnet test -c Release --no-build --filter "TestCategory=LongRunning"` with `env: CURL_RUN_LONG_RUNNING_TESTS: '1'`, `continue-on-error: true`, the same `--logger trx` and `--results-directory`, so `.github/integration/Write-IntegrationTestSummary.ps1` reports them with the Integration results. Read that script; if it assumes a single TRX file, make it read every TRX in the directory. Update the workflow's header comment (it says the Cryptography Integration tests took 27 minutes) to say the long-running known-answer tests run here too. Keep `INTEGRATION_FAILURES_FAIL_THE_RUN`'s behaviour unchanged for both steps.
- `Directory.Build.props`: BL-1603's check carries an allow-list of exactly these four files for `Curl.Cryptography.UnitTests`; remove the allow-list and its item group, and update the comment that named this task.

## Acceptance criteria

- [ ] `grep -rn '^\s*\[.*TestCategory("Integration")' Curl.Cryptography.UnitTests --include=*.cs` finds nothing; each of the four tests named in Context carries `[TestCategory("LongRunning")]` and the condition attribute, unless Notes record it now runs under 3 s in Debug and joined the fast run untagged.
- [ ] `Directory.Build.props` has no allow-list for `Curl.Cryptography.UnitTests`, and `dotnet build -warnaserror` at the repository root is clean.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green, and its `Curl.Cryptography.UnitTests` summary shows the LongRunning tests skipped, not run.
- [ ] With `CURL_RUN_LONG_RUNNING_TESTS=1` set, `dotnet test Curl.Cryptography.UnitTests -c Release --filter "TestCategory=LongRunning"` runs them and all pass; Notes record each one's duration.
- [ ] `.github/workflows/integration.yml` has the LongRunning step with the variable set, and its header comment and `Write-IntegrationTestSummary.ps1` (if changed) are true of what they do.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary` reports 100% line and 100% branch coverage for `Curl.Cryptography.UnitLibrary`.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
