---
id: BL-4762
title: Fix AF-0598: Surviving mutant: --csharp/--powershell files can be written with a UTF-8 BOM without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4762 — Fix AF-0598: Surviving mutant: --csharp/--powershell files can be written with a UTF-8 BOM without a test failing

## Goal

The defect the audit office reported as AF-0598 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0598 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0598-surviving-mutant-csharp-powershell-files-can-be-wr.md`.

Location: `Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs:35`

Location: `Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs:35`

Drawn library. new UTF8Encoding(false) -> new UTF8Encoding(true) survived: the generated file then starts with EF BB BF, a change to the output bytes. ExecuteAsync_EachTransfer_WritesEveryRequestSoFarThenRunsTheTransferUnchanged reads the file with File.ReadAllTextAsync, which strips a BOM.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs:35:false -Member WriteAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
