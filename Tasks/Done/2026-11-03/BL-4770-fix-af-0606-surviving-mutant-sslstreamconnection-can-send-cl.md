---
id: BL-4770
title: Fix AF-0606: Surviving mutant: SslStreamConnection can send close_notify more than once without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/CloseNotifyOnDisposeTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4770 — Fix AF-0606: Surviving mutant: SslStreamConnection can send close_notify more than once without a test failing

## Goal

The defect the audit office reported as AF-0606 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0606 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0606-surviving-mutant-sslstreamconnection-can-send-clos.md`.

Location: `Curl.Networking.UnitLibrary/SslStreamConnection.cs:202`

Location: `Curl.Networking.UnitLibrary/SslStreamConnection.cs:202`

_tlsEnded = true -> false survived (sample and targeted rerun, with the 102 baseline-failing tests left out): a later close or clear then shuts TLS down again, a second alert on the wire.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SslStreamConnection.cs:202:true -Member SendCloseNotifyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was already right: `SendCloseNotifyAsync` sets `_tlsEnded` before shutting TLS down. What was missing was a test that fails when a second dispose tries again. `SslStream` refuses a second `ShutdownAsync` itself, so nothing different reaches the wire. The new test `CloseNotifyOnDisposeTests.DisposeAsync_OfAnSslStreamConnectionCalledTwice_TriesToSendCloseNotifyOnlyOnce` counts the close_notify timers created on the injected `TimeProvider` instead: each attempt creates one.
- Mutation check: a lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand. With `_tlsEnded = true` changed to `false` at SslStreamConnection.cs:202, the new test fails (1 failed, 3 passed in CloseNotifyOnDisposeTests). With the line restored, the build is clean and Curl.Networking.UnitTests' fast tests pass (4018 passed, 34 skipped). Production code is unchanged, so there was no Measure-CodeQuality run.
- Added `Curl.Networking.UnitTests/CloseNotifyOnDisposeTests.cs` to `touches` because the test lives there. BL-4749 in Doing names the whole `Curl.Networking.UnitTests` project, but its work is `ResolveOverridesTests.cs`, so the two tasks change different files and no dependency was added.
- The test is platform-neutral: it uses an in-memory TLS pair and the system clock, with no paths and no OS error text.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A second dispose of SslStreamConnection is pinned to one close_notify attempt; the AF-0606 mutant is killed
