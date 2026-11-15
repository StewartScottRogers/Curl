---
id: AF-0795
title: Surviving mutant: a URL host of exactly 65535 bytes can be refused with exit 3
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:ParsedUrlRefusal-gt:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:171:>
task: none
tasks:
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0795 - Surviving mutant: a URL host of exactly 65535 bytes can be refused with exit 3

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:171`: Surviving mutant: a URL host of exactly 65535 bytes can be refused with exit 3.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:171`

Mutant 'GetByteCount(url.Host) > MaximumHostLength' -> '>=' in ParsedUrlRefusal survived (seed 5851). MaximumHostLength is 65535 (line 59), and curl accepts hosts up to that length. The mutant refuses one of exactly 65535 bytes with CurlExitCode.UrlMalformat and TooLongHostnameMessage, changing the exit code. No test exercises the boundary.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:171:> -Member ParsedUrlRefusal -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
