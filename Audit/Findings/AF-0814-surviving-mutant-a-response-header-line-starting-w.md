---
id: AF-0814
title: Surviving mutant: a response header line starting with ':' can be dropped from curl_easy_header's store
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/TransferHeaderStore.cs:Add-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferHeaderStore.cs:277:>=
task: BL-5268
tasks: BL-5268
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed: 2026-11-16
closed-how: mechanical
closed-by: 2026-11-16_1333.md
---
# AF-0814 - Surviving mutant: a response header line starting with ':' can be dropped from curl_easy_header's store

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferHeaderStore.cs:277`: Surviving mutant: a response header line starting with ':' can be dropped from curl_easy_header's store.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferHeaderStore.cs:277`

`if (colon >= 0)` changed to `if (colon > 0)` survived. A line such as ':x' (colon at index 0) is stored as a header with an empty name today; with the mutant it is dropped, changing what curl_easy_header and curl_easy_nextheader return. No test feeds a line with a leading colon.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferHeaderStore.cs:277:>= -Member Add -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-16 | 2026-11-16_1333.md | reproduces: no | Ran the -Site command: resolvedLine 277 ('colon >= 0' to '> 0'), outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-16: filed proposed.
- 2026-11-16: accepted -> closed. Re-audit 2026-11-16_1333.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
