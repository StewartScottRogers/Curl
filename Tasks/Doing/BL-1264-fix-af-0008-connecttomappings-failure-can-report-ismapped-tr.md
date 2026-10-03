---
id: BL-1264
title: Fix AF-0008: ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1264 — Fix AF-0008: ConnectToMappings.Failure can report IsMapped: true instead of false with no test failing

## Goal

The defect the audit office reported as AF-0008 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0008 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0008-connecttomappings-failure-can-report-ismapped-true.md`.

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:135`

Location: `Curl.Networking.UnitLibrary/ConnectToMappings.cs:135`

Mutant survived (seed 0): `new(string.Empty, 0, IsMapped: false, message)` became `IsMapped: true`. A failed --connect-to parse could then claim a mapping, and no test checks IsMapped on a failure result.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at Curl.Networking.UnitLibrary/ConnectToMappings.cs:135 (false) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/ConnectToMappings.cs:135 false

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
