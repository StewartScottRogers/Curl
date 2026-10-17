---
id: BL-2565
title: Fix AF-0228: Nine TransferWriteOutVariables tests check their rendered -w text only through Show, which never asserts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitTests]
requirement: none
created: 2026-10-17
completed:
---
# BL-2565 — Fix AF-0228: Nine TransferWriteOutVariables tests check their rendered -w text only through Show, which never asserts

## Goal

The defect the audit office reported as AF-0228 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0228 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0228-nine-transferwriteoutvariables-tests-check-their-r.md`.

Location: `Curl.Output.UnitTests/TransferWriteOutVariablesFromTransferInfoTests.cs:218`

Location: `Curl.Output.UnitTests/TransferWriteOutVariablesFromTransferInfoTests.cs:218`

private static void Show(TestDiagnostics diagnostics, string expected, string actual) { diagnostics.Act("rendered", actual); diagnostics.Assert("rendered", expected, actual); } - and TestDiagnostics.Assert (TestDiagnostics.cs:189) only increments AssertCount and writes an ASSERT line; it never throws. TryGetVariableText_Times_PrintTheInfoTimesInMicroseconds, _Sizes_, _Speeds_, _Addresses_, _Http_PrintsTheInfoCodesVersionMethodTypeAndScheme, _Tls_, _Redirects_, _Ids_ and InfoProperties_Set_ReplaceTheInfoValuesTheyReadBack (lines 21-191) end with Show(...) and nothing else, so any rendering of time_total, http_version, url_effective, conn_id and the rest passes. The whole file has one real assertion, Assert.IsTrue(variables.TransferFailed) at line 169. These names promise the exact -w output bytes. Found by Find-WeakTests (no-assertion) and confirmed by reading the helper.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitTests/TransferWriteOutVariablesFromTransferInfoTests.cs -Pattern '^\s*(Assert|CollectionAssert|StringAssert)\.' | Measure-Object | Select-Object -ExpandProperty Count
```

- Expected: At least 10: each TryGetVariableText_* test asserts its rendered text, for example with Assert.AreEqual.
- Actual: 1 (only Assert.IsTrue(variables.TransferFailed)); Show calls only diagnostics.Assert, which logs.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
