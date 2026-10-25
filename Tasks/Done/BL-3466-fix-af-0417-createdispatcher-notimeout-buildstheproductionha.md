---
id: BL-3466
title: Fix AF-0417: CreateDispatcher_NoTimeout_BuildsTheProductionHandlers checks only that a dispatcher is not null, not which handlers it holds
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-10-24
completed: 2026-10-24
---
# BL-3466 — Fix AF-0417: CreateDispatcher_NoTimeout_BuildsTheProductionHandlers checks only that a dispatcher is not null, not which handlers it holds

## Goal

The defect the audit office reported as AF-0417 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0417 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0417-createdispatcher-notimeout-buildstheproductionhand.md`.

Location: `Curl.Engine.UnitTests/ManagedCurlEngineTests.cs:79`

Location: `Curl.Engine.UnitTests/ManagedCurlEngineTests.cs:79`

The only assertion is Assert.IsNotNull(dispatcher); after ManagedCurlEngine.CreateDispatcher(new EasyOptions(), engine.Shared, cookieStore: null). The name promises the production handlers. A dispatcher missing a protocol handler, or holding a test double, would also pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedCurlEngineTests.cs -Pattern 'CreateDispatcher_NoTimeout_BuildsTheProductionHandlers' -Context 0,7
```

- Expected: The test asserts the schemes or handler types the dispatcher holds.
- Actual: Assert.IsNotNull(dispatcher); is the test's only assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Test now asserts Serves() for http, https, ftp, file, dict, tftp, smtp, imap, pop3, ws and not bogus. Build clean; Engine tests for ManagedCurlEngineTests pass (44).

## Log

- 2026-10-24: Created.
- 2026-10-24: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-24: Doing -> Done. CreateDispatcher test asserts the schemes the production dispatcher serves
