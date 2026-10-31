---
id: BL-4399
title: Fix AF-0489: Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven never checks that the hook stays off without the option
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Har.UnitTests]
requirement: none
created: 2026-10-31
completed:
---
# BL-4399 — Fix AF-0489: Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven never checks that the hook stays off without the option

## Goal

The defect the audit office reported as AF-0489 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0489 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0489-composition-activatesthehookonlywhentheoptionisgiv.md`.

Location: `Curl.Extension.Har.UnitTests/HarExtensionTests.cs:36`

Location: `Curl.Extension.Har.UnitTests/HarExtensionTests.cs:36`

The whole body is Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<HarTransferHook>().Any()); with Given() supplying --har out.har. The 'only when' half of the name, that no HarTransferHook is active without the option, is never exercised. A composition that always activated the HAR hook (writing HAR output on every transfer) would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.Har.UnitTests/HarExtensionTests.cs -Pattern 'Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven' -Context 0,3
```

- Expected: The test also asserts that ActivatedBy with no --har value yields no HarTransferHook.
- Actual: One assertion only: Assert.IsTrue(... ActivatedBy(Given(), ...).Hooks.OfType<HarTransferHook>().Any());

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
