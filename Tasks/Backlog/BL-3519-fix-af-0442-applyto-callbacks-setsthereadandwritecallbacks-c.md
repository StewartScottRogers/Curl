---
id: BL-3519
title: Fix AF-0442: ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks checks only that both callbacks are not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests]
requirement: none
created: 2026-10-25
completed:
---
# BL-3519 — Fix AF-0442: ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks checks only that both callbacks are not null

## Goal

The defect the audit office reported as AF-0442 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0442 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0442-applyto-callbacks-setsthereadandwritecallbacks-che.md`.

Location: `Curl.Api.UnitTests/Stores/CurlHstsStoreTests.cs:113`

Location: `Curl.Api.UnitTests/Stores/CurlHstsStoreTests.cs:113`

Body: 'EasyCallbackOptions callbacks = new CurlHstsStore().ApplyTo(new EasyCallbackOptions());' then 'Assert.IsNotNull(callbacks.HstsRead);' and 'Assert.IsNotNull(callbacks.HstsWrite);'. ApplyTo could set callbacks over another store, or ones that ignore the store, and this test would still pass. The other tests drive CreateReadCallback directly, not the callbacks ApplyTo sets. Found by Find-WeakTests.ps1 (weak-assertion) and confirmed by reading the test.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Api.UnitTests/Stores/CurlHstsStoreTests.cs -Pattern 'ApplyTo_Callbacks_SetsTheReadAndWriteCallbacks' -Context 0,7
```

- Expected: The test drives the callbacks ApplyTo set and checks they read and write this store's entries.
- Actual: Only Assert.IsNotNull(callbacks.HstsRead) and Assert.IsNotNull(callbacks.HstsWrite).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
