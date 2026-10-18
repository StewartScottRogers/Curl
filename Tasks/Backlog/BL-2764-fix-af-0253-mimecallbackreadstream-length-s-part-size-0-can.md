---
id: BL-2764
title: Fix AF-0253: MimeCallbackReadStream.Length's part.Size >= 0 can become > 0 with no test failing, so an empty sized callback part throws
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-18
completed:
---
# BL-2764 — Fix AF-0253: MimeCallbackReadStream.Length's part.Size >= 0 can become > 0 with no test failing, so an empty sized callback part throws

## Goal

The defect the audit office reported as AF-0253 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0253 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0253-mimecallbackreadstream-length-s-part-size-0-can-be.md`.

Location: `Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs:39`

Location: `Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs:39`

Engine sample, seed 0: survived. Original `CanSeek && part.Size >= 0 ? part.Size : throw new NotSupportedException()`, mutated `> 0`. A seekable mime callback part of declared size 0 would throw NotSupportedException instead of reporting length 0, which changes how its Content-Length is computed. No test covers the size-0 edge.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MimeCallbackReadStream.cs:39:>= -Member Length -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
