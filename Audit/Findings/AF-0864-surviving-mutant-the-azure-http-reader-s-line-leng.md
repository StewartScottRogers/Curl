---
id: AF-0864
title: Surviving mutant: the Azure HTTP reader's line-length limit boundary is untested
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs:ReadLineAsync-gt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs:29:>
task: BL-5506
tasks: BL-5506
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0134.md
---
# AF-0864 - Surviving mutant: the Azure HTTP reader's line-length limit boundary is untested

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs:29`: Surviving mutant: the Azure HTTP reader's line-length limit boundary is untested. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs:29`

Mutant 'if (line.WrittenCount + take > MaxLineLength)' -> '>=' survived. A response line of exactly MaxLineLength (65536) bytes would be refused as LineTooLong instead of read. The only line-length test (SendAsync_LineLongerThanTheLimit_FailsLineTooLong) uses a line far past the limit, so the boundary is unchecked and the transfer's refusal can move.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs:29:> -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Targeted run Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpConnectionReader.cs:29:> (resolvedLine 29) gave outcome killed. This is the rerun result after a worktree-name collision. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
