---
id: AF-0228
title: Nine TransferWriteOutVariables tests check their rendered -w text only through Show, which never asserts
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Output.UnitTests/TransferWriteOutVariablesFromTransferInfoTests.cs:Show:no-assertion
reproduction: none
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0228 - Nine TransferWriteOutVariables tests check their rendered -w text only through Show, which never asserts

## Summary

Medium finding from the quality auditor at `Curl.Output.UnitTests/TransferWriteOutVariablesFromTransferInfoTests.cs:218`: Nine TransferWriteOutVariables tests check their rendered -w text only through Show, which never asserts. Reported by an auditor flagged unreliable in 2026-10-16_1623.md.

## Evidence

Location: `Curl.Output.UnitTests/TransferWriteOutVariablesFromTransferInfoTests.cs:218`

private static void Show(TestDiagnostics diagnostics, string expected, string actual) { diagnostics.Act("rendered", actual); diagnostics.Assert("rendered", expected, actual); } - and TestDiagnostics.Assert (TestDiagnostics.cs:189) only increments AssertCount and writes an ASSERT line; it never throws. TryGetVariableText_Times_PrintTheInfoTimesInMicroseconds, _Sizes_, _Speeds_, _Addresses_, _Http_PrintsTheInfoCodesVersionMethodTypeAndScheme, _Tls_, _Redirects_, _Ids_ and InfoProperties_Set_ReplaceTheInfoValuesTheyReadBack (lines 21-191) end with Show(...) and nothing else, so any rendering of time_total, http_version, url_effective, conn_id and the rest passes. The whole file has one real assertion, Assert.IsTrue(variables.TransferFailed) at line 169. These names promise the exact -w output bytes. Found by Find-WeakTests (no-assertion) and confirmed by reading the helper.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitTests/TransferWriteOutVariablesFromTransferInfoTests.cs -Pattern '^\s*(Assert|CollectionAssert|StringAssert)\.' | Measure-Object | Select-Object -ExpandProperty Count
```

- Expected: At least 10: each TryGetVariableText_* test asserts its rendered text, for example with Assert.AreEqual.
- Actual: 1 (only Assert.IsTrue(variables.TransferFailed)); Show calls only diagnostics.Assert, which logs.

## Re-audits

## Log

- 2026-10-16: filed proposed.
