---
id: AF-0814
title: Surviving mutant: a response header line starting with ':' can be dropped from curl_easy_header's store
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/TransferHeaderStore.cs:Add-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferHeaderStore.cs:277:>=
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-16: filed proposed.
