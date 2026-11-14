---
id: AF-0715
title: Surviving mutant: CURLINFO_CONTENT_LENGTH_UPLOAD can report the fields' length when POSTFIELDSIZE is 0
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:ContentLengthUploadOf-lt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:88:<
task: BL-5074
tasks: BL-5074
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: mechanical
closed-by: 2026-11-13_2321.md
---
# AF-0715 - Surviving mutant: CURLINFO_CONTENT_LENGTH_UPLOAD can report the fields' length when POSTFIELDSIZE is 0

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:88`: Surviving mutant: CURLINFO_CONTENT_LENGTH_UPLOAD can report the fields' length when POSTFIELDSIZE is 0. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:88`

Mutant 'PostFieldSize < 0' -> 'PostFieldSize <= 0' survived. With POSTFIELDS set and POSTFIELDSIZE explicitly 0, the original reports 0 (the next arm) and the mutant reports fields.Length. getinfo's upload content length is not pinned at that boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:88:< -Member ContentLengthUploadOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Ran the -Site command: outcome killed (PostFieldSize < 0 -> <= 0). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
