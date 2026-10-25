---
id: BL-3493
title: Fix AF-0427: DerCertificateFile.LongFormValueEnd's headerLength > contents.Length can become >= with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-25
completed:
---
# BL-3493 — Fix AF-0427: DerCertificateFile.LongFormValueEnd's headerLength > contents.Length can become >= with no test failing

## Goal

The defect the audit office reported as AF-0427 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0427 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0427-dercertificatefile-longformvalueend-s-headerlength.md`.

Location: `Curl.Networking.UnitLibrary/DerCertificateFile.cs:109`

Location: `Curl.Networking.UnitLibrary/DerCertificateFile.cs:109`

Sampled mutant (seed 0) survived: 'if (headerLength > contents.Length)' -> 'if (headerLength >= contents.Length)'. When a DER file ends exactly after a long-form length header (e.g. 30 81 00, or 04 81 00), the original computes valueEnd == contents.Length and OuterValueError goes on to the tag check / certificate load (OpenSslWrongTag); the mutant returns long.MaxValue and reports OpenSslNotEnoughData. The error message curl prints for such a --cert/--cacert file changes, and no test pins the boundary where the header fills the file.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DerCertificateFile.cs:109:> -Member LongFormValueEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
