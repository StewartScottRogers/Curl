---
id: BL-1868
title: Fix AF-0118: DerCertificateFile.OuterValueEnd's 'contents.Length < 2' can become '<= 2' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1868 — Fix AF-0118: DerCertificateFile.OuterValueEnd's 'contents.Length < 2' can become '<= 2' with no test failing

## Goal

The defect the audit office reported as AF-0118 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0118 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0118-dercertificatefile-outervalueend-s-contents-length.md`.

Location: `Curl.Networking.UnitLibrary/DerCertificateFile.cs:85`

Location: `Curl.Networking.UnitLibrary/DerCertificateFile.cs:85`

Mutant 'if (contents.Length < 2)' -> 'if (contents.Length <= 2)' survived. For a 2-byte DER file (a tag and a zero length, e.g. 30 00 or 04 00) the original reads the length and goes on to the tag check or certificate parse, while the mutant returns long.MaxValue. OuterValueError (line 73) then reports TlsFailureMessages.OpenSslNotEnoughData instead of OpenSslWrongTag or the parse's message, so the error text of the refusal changes. This is a narrow edge of a refusal message. No test feeds a 2-byte certificate file.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DerCertificateFile.cs:85:< -Member OuterValueEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
