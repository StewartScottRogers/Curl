---
id: BL-5076
title: Fix AF-0717: Surviving mutant: libcurl transfers can trace FTP lines by default
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5076 — Fix AF-0717: Surviving mutant: libcurl transfers can trace FTP lines by default

## Goal

The defect the audit office reported as AF-0717 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0717 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0717-surviving-mutant-libcurl-transfers-can-trace-ftp-l.md`.

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:141`

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:141`

Mutant default 'bool tracesFtp = false' -> 'true' survived. ManagedCurlEngine.cs:241 calls CreateProtocolHandlers without tracesFtp, so the default is what the libcurl path uses. With the mutant every libcurl FTP transfer would emit [FTP] trace lines to its debug callback, and no test pins their absence.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:141:false -Member CreateProtocolHandlers -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
