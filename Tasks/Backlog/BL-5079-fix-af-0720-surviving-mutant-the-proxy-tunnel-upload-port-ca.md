---
id: BL-5079
title: Fix AF-0720: Surviving mutant: the proxy-tunnel upload port can send an empty buffer when it is exactly the data's size
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5079 — Fix AF-0720: Surviving mutant: the proxy-tunnel upload port can send an empty buffer when it is exactly the data's size

## Goal

The defect the audit office reported as AF-0720 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0720 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0720-surviving-mutant-the-proxy-tunnel-upload-port-can.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/LibProxyTunnelUpload.cs:131`

Location: `Curl.Conformance.LibTest.UnitLibrary/LibProxyTunnelUpload.cs:131`

Mutant 'buffer.Length >= Data.Length' -> '>' survived. For a buffer exactly Data.Length long the mutant copies nothing yet still returns Data.Length, so the upstream case would upload zero bytes in place of the body. The exact-size read is untested.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/LibProxyTunnelUpload.cs:131:>= -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
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
