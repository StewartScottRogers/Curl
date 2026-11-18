---
id: AF-0862
title: Surviving mutant: an Azure Storage 300 response is taken as success
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs:ExchangeAsync-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs:60:>=
task: BL-5504
tasks: BL-5504
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0862 - Surviving mutant: an Azure Storage 300 response is taken as success

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs:60`: Surviving mutant: an Azure Storage 300 response is taken as success. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs:60`

Mutant 'if (response.StatusCode >= 300)' -> 'if (response.StatusCode > 300)' survived Curl.Extended.Protocol.AzureStorage.UnitTests (mutation-Curl.Extended.Protocol.AzureStorage.UnitLibrary.json). A 300 Multiple Choices reply would then be read as the object (written to stdout, exit 0) instead of writing the error body and failing with exit 22 through ErrorOf. That changes the exit code and the output bytes, and no test uses status 300.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs:60:>= -Member ExchangeAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-18: filed proposed.
