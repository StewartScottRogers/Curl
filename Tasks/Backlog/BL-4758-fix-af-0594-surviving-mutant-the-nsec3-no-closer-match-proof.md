---
id: BL-4758
title: Fix AF-0594: Surviving mutant: the NSEC3 no-closer-match proof can test the wrong ancestor without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Dane.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4758 — Fix AF-0594: Surviving mutant: the NSEC3 no-closer-match proof can test the wrong ancestor without a test failing

## Goal

The defect the audit office reported as AF-0594 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0594 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0594-surviving-mutant-the-nsec3-no-closer-match-proof-c.md`.

Location: `Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:66`

Location: `Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:66`

Drawn library. Ancestor(name, signatureLabels + 1) -> signatureLabels - 1 survived: the wildcard-answer proof (RFC 5155 section 8.8) then checks coverage of the wrong name, a DNSSEC validation decision that decides whether a DANE lookup is Secure or Bogus. The single test ProvesNoCloserMatch_Nsec3CoversTheNextCloserName_IsTrue uses an NSEC3 covering every hash, so it cannot tell.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:66:+1 -Member ProvesNoCloserMatch -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
