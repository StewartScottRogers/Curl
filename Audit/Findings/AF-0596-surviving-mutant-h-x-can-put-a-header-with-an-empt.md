---
id: AF-0596
title: Surviving mutant: -H ':x' can put a header with an empty name into generated code without a test failing
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:AddGiven-gt:surviving-mutant
reproduction: mutation Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134:>
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0596 - Surviving mutant: -H ':x' can put a header with an empty name into generated code without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134`: Surviving mutant: -H ':x' can put a header with an empty name into generated code without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134`

Drawn library. if (colon > 0) -> colon >= 0 survived: a -H line starting with ':' is then added as a field with an empty name, so the generated C# or PowerShell holds a header curl never sends. From_TheOptionsThatSetFields_GiveThoseFieldsUnlessHGaveThem covers 'Host:' and 'NoSeparator' but no leading colon.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134:> -Member AddGiven -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
