---
id: AF-0255
title: TryLookUpNetrcCredentials' || can become && with no test failing, so tool-driven or netrc-ignored transfers still read .netrc
auditor: quality
severity: Medium
status: closed
reason: Fixed by BL-2766 (Done 2026-10-18): TryLookUpNetrcCredentials was rewritten into TryLookUpUrlAndNetrcCredentials and TransferCredentialLookup, so the mutant site no longer exists and three re-audits could only report site-missing.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:TryLookUpNetrcCredentials-or:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:707:||
task: BL-2766
tasks: BL-2766
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed: 2026-10-22
closed-how: stewart
closed-by: stewart
---
# AF-0255 - TryLookUpNetrcCredentials' || can become && with no test failing, so tool-driven or netrc-ignored transfers still read .netrc

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:707`: TryLookUpNetrcCredentials' || can become && with no test failing, so tool-driven or netrc-ignored transfers still read .netrc. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:707`

Engine sample, seed 0: survived. Original `if (ToolInputs is not null || options.Authentication.Netrc == NetrcUsage.Ignored) return true;`, mutated `&&`. With the mutant, a transfer with ToolInputs and netrc enabled, or one with netrc Ignored and no ToolInputs, runs the engine's netrc lookup it should skip. That can add credentials, an info line, or an exit-26 failure. No test pins the skip.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:707:|| -Member TryLookUpNetrcCredentials -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: resolved to line 744 in TryLookUpNetrcCredentials, ToolInputs is not null || Netrc == Ignored -> && survived. Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-10-19 | 2026-10-19_2134.md | not re-audited | Ran the -Site command: outcome site-missing (resolvedLine null). The member is now TryLookUpUrlAndNetrcCredentials (ManagedEasyTransfer.cs:764), which holds no || (the netrc-ignored test moved into TransferCredentialLookup), so the mutant could not be run. Not re-audited.
- 2026-10-22 | 2026-10-22_0130.md | not re-audited | Ran the -Site reproduction: outcome site-missing (no mutant ran). TryLookUpNetrcCredentials no longer exists. It is now TryLookUpUrlAndNetrcCredentials (ManagedEasyTransfer.cs:764), which holds no || and hands the decision to TransferCredentialLookup.TryLookUp, so the site cannot be rerun. Not re-audited.
- 2026-10-22 | 2026-10-22_1120.md | not re-audited | Ran the reproduction: outcome site-missing, no mutant ran. TryLookUpNetrcCredentials no longer exists in Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs; the lookup is now TryLookUpUrlAndNetrcCredentials (line 764), and the netrc-ignored decision is in TransferCredentialLookup.cs (line 112). Not re-audited.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
- 2026-10-22: accepted -> closed. Stewart closed it: Fixed by BL-2766 (Done 2026-10-18): TryLookUpNetrcCredentials was rewritten into TryLookUpUrlAndNetrcCredentials and TransferCredentialLookup, so the mutant site no longer exists and three re-audits could only report site-missing.
