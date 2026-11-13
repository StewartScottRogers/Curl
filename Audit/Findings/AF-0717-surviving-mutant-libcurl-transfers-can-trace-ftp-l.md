---
id: AF-0717
title: Surviving mutant: libcurl transfers can trace FTP lines by default
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/EngineComposition.cs:CreateProtocolHandlers-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineComposition.cs:141:false
task: BL-5076
tasks: BL-5076
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0717 - Surviving mutant: libcurl transfers can trace FTP lines by default

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/EngineComposition.cs:141`: Surviving mutant: libcurl transfers can trace FTP lines by default. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:141`

Mutant default 'bool tracesFtp = false' -> 'true' survived. ManagedCurlEngine.cs:241 calls CreateProtocolHandlers without tracesFtp, so the default is what the libcurl path uses. With the mutant every libcurl FTP transfer would emit [FTP] trace lines to its debug callback, and no test pins their absence.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:141:false -Member CreateProtocolHandlers -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
