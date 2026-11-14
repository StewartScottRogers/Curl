---
id: AF-0725
title: Surviving mutant: a 1024-character attribute can be dropped from the WinLDAP search request
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs:ReadAttributes-gt:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs:96:>
task: BL-5084
tasks: BL-5084
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
---
# AF-0725 - Surviving mutant: a 1024-character attribute can be dropped from the WinLDAP search request

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs:96`: Surviving mutant: a 1024-character attribute can be dropped from the WinLDAP search request. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs:96`

Mutant 'attribute.Length is 0 or > MaxAttributeLength' (MaxAttributeLength = 1024) -> '>=' survived. An attribute of exactly 1024 characters is kept by the original and dropped by the mutant (the loop breaks), which changes the SearchRequest bytes sent. The limit's boundary is untested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Ldap.UnitLibrary/WinLdapUrlReader.cs:96:> -Member ReadAttributes -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (attribute.Length is 0 or > MaxAttributeLength -> >=). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
