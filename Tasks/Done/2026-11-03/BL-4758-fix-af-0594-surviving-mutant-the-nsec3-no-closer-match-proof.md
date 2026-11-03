---
id: BL-4758
title: Fix AF-0594: Surviving mutant: the NSEC3 no-closer-match proof can test the wrong ancestor without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Dane.UnitLibrary, Curl.Extension.Dane.UnitTests/Lookup/DenialOfExistenceTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Widened `touches` to `Curl.Extension.Dane.UnitTests/Lookup/DenialOfExistenceTests.cs`, because the fix is a test. No other Doing task on origin/work/dark-factory names a Dane project.
- Added two tests. `ProvesNoCloserMatch_Nsec3CoversOnlyTheNextCloserName_IsTrue_Rfc5155Section8_8` uses an NSEC3 whose range is the next closer name's hash (b.example.) plus or minus one, so it covers nothing else. `ProvesNoCloserMatch_Nsec3CoversOnlyAnotherAncestor_IsFalse_Rfc5155Section8_8` covers only the apex hash and must not prove the match.
- The lane may not run the audit office's mutation tool, so the mutant was checked by hand. With `signatureLabels - 1` at `DenialOfExistence.cs:66` the first new test fails (killed). With production restored all three ProvesNoCloserMatch tests pass. There is no production change, so no quality run was needed. `dotnet build` is clean (0 warnings) and the fast tests are green. The re-audit closes the finding.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Tests kill the NSEC3 no-closer-match wrong-ancestor mutant
