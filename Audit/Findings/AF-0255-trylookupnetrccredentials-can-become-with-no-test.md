---
id: AF-0255
title: TryLookUpNetrcCredentials' || can become && with no test failing, so tool-driven or netrc-ignored transfers still read .netrc
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:TryLookUpNetrcCredentials-or:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:707:||
task: BL-2766
tasks: BL-2766
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
