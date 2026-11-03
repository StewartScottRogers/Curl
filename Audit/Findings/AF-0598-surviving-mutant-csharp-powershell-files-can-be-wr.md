---
id: AF-0598
title: Surviving mutant: --csharp/--powershell files can be written with a UTF-8 BOM without a test failing
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs:WriteAsync-false:surviving-mutant
reproduction: mutation Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs:35:false
task: BL-4762
tasks: BL-4762
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0598 - Surviving mutant: --csharp/--powershell files can be written with a UTF-8 BOM without a test failing

## Summary

High finding from the quality auditor at `Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs:35`: Surviving mutant: --csharp/--powershell files can be written with a UTF-8 BOM without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs:35`

Drawn library. new UTF8Encoding(false) -> new UTF8Encoding(true) survived: the generated file then starts with EF BB BF, a change to the output bytes. ExecuteAsync_EachTransfer_WritesEveryRequestSoFarThenRunsTheTransferUnchanged reads the file with File.ReadAllTextAsync, which strips a BOM.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CodeGeneration.UnitLibrary/CodeGenerationTransferHook.cs:35:false -Member WriteAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
