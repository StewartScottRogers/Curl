---
id: BL-5078
title: Fix AF-0719: Surviving mutant: --capath can take a file named 'xxxxxxxx.' (no digit) as a hashed certificate
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-13
completed:
---
# BL-5078 — Fix AF-0719: Surviving mutant: --capath can take a file named 'xxxxxxxx.' (no digit) as a hashed certificate

## Goal

The defect the audit office reported as AF-0719 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0719 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0719-surviving-mutant-capath-can-take-a-file-named-xxxx.md`.

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:698`

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:698`

Mutant 'name is { Length: > 9 }' -> '>= 9' survived. A 9-character name such as 'abcdef01.' passes every other check (the dot at index 8, hex before it, and an empty suffix contains no non-digit), so the mutant counts it among the CA directory's subject hashes, which changes trust decisions. The boundary is untested.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:698:> -Member IsHashedCertificateFileName -ExcludeBaselineFailures -TimeoutSeconds 600
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
