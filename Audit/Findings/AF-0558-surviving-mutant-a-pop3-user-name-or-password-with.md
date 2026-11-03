---
id: AF-0558
title: Surviving mutant: a POP3 user name or password with a space can be refused with exit 43 without a test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs:HasControlByte-lt:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs:87:<
task: BL-4706
tasks: BL-4706
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1017.md
---
# AF-0558 - Surviving mutant: a POP3 user name or password with a space can be refused with exit 43 without a test failing

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs:87`: Surviving mutant: a POP3 user name or password with a space can be refused with exit 43 without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs:87`

Mutant 'character < ' '' -> 'character <= ' '' in HasControlByte survived. TransferAsync refuses credentials for which it holds with CurlExitCode.BadFunctionArgument before connecting, so the mutant makes a password such as 'a b' fail with exit 43 and send nothing. No test pins a credential holding a space.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Pop3.UnitLibrary/Pop3ProtocolHandler.cs:87:< -Member HasControlByte -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | Ran the -Site command: outcome killed (resolvedLine 87). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
