---
id: AF-0489
title: Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven never checks that the hook stays off without the option
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Extension.Har.UnitTests/HarExtensionTests.cs:Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0489 - Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven never checks that the hook stays off without the option

## Summary

Medium finding from the quality auditor at `Curl.Extension.Har.UnitTests/HarExtensionTests.cs:36`: Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven never checks that the hook stays off without the option.

## Evidence

Location: `Curl.Extension.Har.UnitTests/HarExtensionTests.cs:36`

The whole body is Assert.IsTrue(TransferHooks.ActivatedBy(Given(), ExtensionTransferHookCatalogue.Entries).Hooks.OfType<HarTransferHook>().Any()); with Given() supplying --har out.har. The 'only when' half of the name, that no HarTransferHook is active without the option, is never exercised. A composition that always activated the HAR hook (writing HAR output on every transfer) would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.Har.UnitTests/HarExtensionTests.cs -Pattern 'Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven' -Context 0,3
```

- Expected: The test also asserts that ActivatedBy with no --har value yields no HarTransferHook.
- Actual: One assertion only: Assert.IsTrue(... ActivatedBy(Given(), ...).Hooks.OfType<HarTransferHook>().Any());

## Re-audits

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
