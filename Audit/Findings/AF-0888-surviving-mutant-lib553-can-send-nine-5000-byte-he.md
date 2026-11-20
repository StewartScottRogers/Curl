---
id: AF-0888
title: Surviving mutant: lib553 can send nine 5000-byte headers instead of upstream's eight
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib553.cs:TestAsync-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib553.cs:44:<
task: BL-5559
tasks: BL-5559
found: 2026-11-19
found-at: 6e68cb60b626d421113f6d7d7f35c2a46bbc3b46
scorecard: 2026-11-19_0602.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_1633.md
---
# AF-0888 - Surviving mutant: lib553 can send nine 5000-byte headers instead of upstream's eight

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib553.cs:44`: Surviving mutant: lib553 can send nine 5000-byte headers instead of upstream's eight.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib553.cs:44`

Mutant for (int i = 0; i < 8; i++) -> for (int i = 0; i <= 8; i++) in TestAsync survived all Curl.Conformance.LibTest.UnitTests (seed 446316). It adds a Header8 line of 5000 bytes to the request lib553 sends, so its request bytes no longer match upstream's lib553.c. This is the conformance harness's libtest program, so the bug reaches the upstream-case ratchet rather than curl's users, but its own unit tests do not pin the header count.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib553.cs:44:< -Member TestAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_1633.md | reproduces: no | Ran the -Site command: Lib553.cs:44 'for (int i = 0; i < 8; i++)' -> 'i <= 8' at resolvedLine 44, outcome killed (killed=1, survived=0). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-19: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_1633.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
