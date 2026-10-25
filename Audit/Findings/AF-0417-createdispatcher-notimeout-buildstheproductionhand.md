---
id: AF-0417
title: CreateDispatcher_NoTimeout_BuildsTheProductionHandlers checks only that a dispatcher is not null, not which handlers it holds
auditor: quality
severity: Low
status: accepted
reason: Stewart accepted it on 2026-10-24.
key: quality:Curl.Engine.UnitTests/ManagedCurlEngineTests.cs:CreateDispatcher_NoTimeout_BuildsTheProductionHandlers:weak-assertion
reproduction: none
task: BL-3466
tasks: BL-3466
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0417 - CreateDispatcher_NoTimeout_BuildsTheProductionHandlers checks only that a dispatcher is not null, not which handlers it holds

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/ManagedCurlEngineTests.cs:79`: CreateDispatcher_NoTimeout_BuildsTheProductionHandlers checks only that a dispatcher is not null, not which handlers it holds.

## Evidence

Location: `Curl.Engine.UnitTests/ManagedCurlEngineTests.cs:79`

The only assertion is Assert.IsNotNull(dispatcher); after ManagedCurlEngine.CreateDispatcher(new EasyOptions(), engine.Shared, cookieStore: null). The name promises the production handlers. A dispatcher missing a protocol handler, or holding a test double, would also pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedCurlEngineTests.cs -Pattern 'CreateDispatcher_NoTimeout_BuildsTheProductionHandlers' -Context 0,7
```

- Expected: The test asserts the schemes or handler types the dispatcher holds.
- Actual: Assert.IsNotNull(dispatcher); is the test's only assertion.

## Re-audits

## Log

- 2026-10-24: filed proposed.
- 2026-10-24: proposed -> accepted.
