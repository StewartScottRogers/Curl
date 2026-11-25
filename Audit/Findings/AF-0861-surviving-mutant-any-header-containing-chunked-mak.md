---
id: AF-0861
title: Surviving mutant: any header containing 'chunked' makes the reported upload length -1
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:IsChunkedTransferEncoding-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:132:&&
task: BL-5503
tasks: BL-5503
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0134.md
---
# AF-0861 - Surviving mutant: any header containing 'chunked' makes the reported upload length -1

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:132`: Surviving mutant: any header containing 'chunked' makes the reported upload length -1. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:132`

Mutant 'header.StartsWith("Transfer-Encoding:") && header.Contains("chunked")' -> '||' survived Curl.Engine.UnitTests. With ||, every Transfer-Encoding header, and any custom header whose value holds 'chunked' (e.g. 'X-Mode: chunked'), makes HttpRequestBodyLengthOf answer -1. That value is ContentLengthUploadOf, CURLINFO_CONTENT_LENGTH_UPLOAD, so the getinfo value is wrong for a known-length POST. No test sends a non-Transfer-Encoding header containing 'chunked', or a Transfer-Encoding header without it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:132:&& -Member IsChunkedTransferEncoding -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Targeted run Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:132:&& (resolvedLine 132) gave outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
