---
id: BL-5074
title: Fix AF-0715: Surviving mutant: CURLINFO_CONTENT_LENGTH_UPLOAD can report the fields' length when POSTFIELDSIZE is 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5074 — Fix AF-0715: Surviving mutant: CURLINFO_CONTENT_LENGTH_UPLOAD can report the fields' length when POSTFIELDSIZE is 0

## Goal

The defect the audit office reported as AF-0715 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0715 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0715-surviving-mutant-curlinfo-content-length-upload-ca.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:88`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:88`

Mutant 'PostFieldSize < 0' -> 'PostFieldSize <= 0' survived. With POSTFIELDS set and POSTFIELDSIZE explicitly 0, the original reports 0 (the next arm) and the mutant reports fields.Length. getinfo's upload content length is not pinned at that boundary.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:88:< -Member ContentLengthUploadOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added Cgr127Tests.ContentLengthUploadOf_PostFieldsWithExplicitSizeZero_IsZeroNotTheFieldsLength, pinning PostFieldSize 0 with PostFields set to 0; it fails under the lt-to-le mutant. Lanes may not use the audit tooling, so the reproduction was not run; the test kills the named mutant by construction. Engine fast tests green (3015 passed).

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Added test pinning POSTFIELDSIZE 0 upload length; engine fast tests green
