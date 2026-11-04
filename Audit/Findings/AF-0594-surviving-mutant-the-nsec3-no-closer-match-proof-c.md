---
id: AF-0594
title: Surviving mutant: the NSEC3 no-closer-match proof can test the wrong ancestor without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:ProvesNoCloserMatch-plus1:surviving-mutant
reproduction: mutation Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:66:+1
task: BL-4758
tasks: BL-4758
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
---
# AF-0594 - Surviving mutant: the NSEC3 no-closer-match proof can test the wrong ancestor without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:66`: Surviving mutant: the NSEC3 no-closer-match proof can test the wrong ancestor without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:66`

Drawn library. Ancestor(name, signatureLabels + 1) -> signatureLabels - 1 survived: the wildcard-answer proof (RFC 5155 section 8.8) then checks coverage of the wrong name, a DNSSEC validation decision that decides whether a DANE lookup is Secure or Bogus. The single test ProvesNoCloserMatch_Nsec3CoversTheNextCloserName_IsTrue uses an NSEC3 covering every hash, so it cannot tell.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Dane.UnitLibrary/Lookup/DenialOfExistence.cs:66:+1 -Member ProvesNoCloserMatch -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: DenialOfExistence.cs:66 +1 resolved to line 66, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
