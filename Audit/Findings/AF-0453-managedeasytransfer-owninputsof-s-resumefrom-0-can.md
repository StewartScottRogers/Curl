---
id: AF-0453
title: ManagedEasyTransfer.OwnInputsOf's ResumeFrom > 0 can become >= 0 with no test failing, so a zero resume offset is passed on as a resume
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-31_0358.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:OwnInputsOf-gt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:150:>
task: BL-4176
tasks: BL-4176
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: mechanical
closed-by: 2026-10-31_0358.md
---
# AF-0453 - ManagedEasyTransfer.OwnInputsOf's ResumeFrom > 0 can become >= 0 with no test failing, so a zero resume offset is passed on as a resume

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:150`: ManagedEasyTransfer.OwnInputsOf's ResumeFrom > 0 can become >= 0 with no test failing, so a zero resume offset is passed on as a resume. Reported by an auditor flagged unreliable in 2026-10-29_0606.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:150`

Line 150: `ResumeFrom: options.Transfer.ResumeFrom > 0 ? options.Transfer.ResumeFrom : null,`. Invoke-MutationTest.ps1 -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0 changed `>` to `>=` and every Curl.Engine.UnitTests test passed (survived). The change matters because consumers tell null from 0. Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs:1358 `if (context.ResumeFrom is { } resumeFrom)` takes the resume path, which wins over CURLOPT_RANGE. FileProtocolHandler.cs:680 `context.ResumeFrom is null && context.Range is { } range` ignores the range for an unknown-size source. So under the mutant, a libcurl easy handle with a range on a file:// URL and the default RESUME_FROM of 0 delivers the whole file instead of the range: different output bytes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:150:> -Member OwnInputsOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test sees a zero resume offset passed on as a resume.
- Actual: outcome survived: every Curl.Engine.UnitTests test passed with the mutant.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: no | Ran the -Site reproduction: ManagedEasyTransfer.SetUp.cs:150 `ResumeFrom: options.Transfer.ResumeFrom > 0 ? ... : null` -> `>= 0`, outcome killed (resolvedLine 150). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_0358.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
