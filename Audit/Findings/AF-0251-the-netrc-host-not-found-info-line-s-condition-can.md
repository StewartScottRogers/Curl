---
id: AF-0251
title: The netrc 'host not found' info line's condition can flip (!= to ==) with no test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:TryLookUpInNetrcFile-ne:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142:!=
task: BL-2762
tasks: BL-2762
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-18
closed-how: mechanical
closed-by: 2026-10-18_1905.md
---
# AF-0251 - The netrc 'host not found' info line's condition can flip (!= to ==) with no test failing

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142`: The netrc 'host not found' info line's condition can flip (!= to ==) with no test failing. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142`

Engine sample, seed 0: survived. Original `if (failure is null && result.Outcome != NetrcLookupOutcome.Found)`, mutated `==`. The mutant writes HostNotFoundLine when the host was found and leaves it out when it was not, so the -v standard-error bytes change in both cases. No test pins the line on the engine path.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142:!= -Member TryLookUpInNetrcFile -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran the -Site command: line 142, result.Outcome != Found -> == killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-18: accepted -> closed. Re-audit 2026-10-18_1905.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
