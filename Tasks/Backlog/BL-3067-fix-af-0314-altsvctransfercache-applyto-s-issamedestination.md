---
id: BL-3067
title: Fix AF-0314: AltSvcTransferCache.ApplyTo's IsSameDestination: true can become false with no Curl.Engine.UnitTests test failing, so alt-svc entries to another host are no longer followed
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3067 — Fix AF-0314: AltSvcTransferCache.ApplyTo's IsSameDestination: true can become false with no Curl.Engine.UnitTests test failing, so alt-svc entries to another host are no longer followed

## Goal

The defect the audit office reported as AF-0314 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0314 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0314-altsvctransfercache-applyto-s-issamedestination-tr.md`.

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:144`

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:144`

Sampled mutant (seed 0) survived: line 144 '{ IsSameDestination: true } same => http with { AltSvcRoute = null, Version = SameDestinationVersion(...), TcpFirstAttemptVersion = ... }' with true flipped to false. Under the mutant, an entry for another host or port takes the same-destination arm (AltSvcRoute = null, so the transfer never dials the alternative). A same-destination entry falls through to '{ } other' and gets a route to itself plus SwitchedVersion. This changes where the request goes, the HTTP version sent, and the 'Alt-svc connecting from' -v line, and no test in the twin noticed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:144:true -Member ApplyTo -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
