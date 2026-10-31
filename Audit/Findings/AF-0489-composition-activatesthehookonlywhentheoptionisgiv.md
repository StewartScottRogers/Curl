---
id: AF-0489
title: Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven never checks that the hook stays off without the option
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
key: quality:Curl.Extension.Har.UnitTests/HarExtensionTests.cs:Composition_ActivatesTheHookOnlyWhenTheOptionIsGiven:name-lies
reproduction: none
task: BL-4399
tasks: BL-4399
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-10-31
closed-how: reliable-reaudit
closed-by: 2026-10-31_1149.md
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

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | Ran the Select-String: line 37 now reads 'Assert.IsFalse(TransferHooks.ActivatedBy(ExtensionOptionValues.None, ExtensionTransferHookCatalogue.Entries).Hooks.OfType<HarTransferHook>().Any());', so the test checks that the hook stays off without the option.

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_1149.md: the reproduction no longer reproduces.
