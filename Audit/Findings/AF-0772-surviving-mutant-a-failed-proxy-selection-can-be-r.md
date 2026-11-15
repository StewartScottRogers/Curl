---
id: AF-0772
title: Surviving mutant: a failed proxy selection can be reported as success and the transfer go on
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:TrySelectProxyAndCredentials-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:239:false
task: none
tasks:
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0772 - Surviving mutant: a failed proxy selection can be reported as success and the transfer go on

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:239`: Surviving mutant: a failed proxy selection can be reported as success and the transfer go on. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:239`

Mutant 'return false;' -> 'return true;' survived in the branch taken when TransferProxySelection.TrySelect(...) fails. Its caller, Curl.Console/CurlCommandRunner.cs:4556, proceeds when the method returns true, so a proxy curl refuses would no longer end the transfer with its exit code. The Engine tests of this method (TransferPerformerTests.Setup.cs, TransferPerformerTests.FeatureMask.cs) cover only the not-built-in mask, the scheme refusal and successful selection, never a TrySelect failure.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:239:false -Member TrySelectProxyAndCredentials -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-14: filed proposed.
