---
id: AF-0995
title: Surviving mutant: an Azure Storage response field line starting with whitespace can be accepted as a field (>= -> >)
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs:ParseField-ge:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs:31:>=
task: BL-5989
tasks: BL-5989
found: 2026-11-25
found-at: eb33fe3c5ab162945003079cf4f807e5608dde01
scorecard: 2026-11-25_1306.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0995 - Surviving mutant: an Azure Storage response field line starting with whitespace can be accepted as a field (>= -> >)

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs:31`: Surviving mutant: an Azure Storage response field line starting with whitespace can be accepted as a field (>= -> >).

## Evidence

Location: `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs:31`

Mutant 'line.AsSpan(0, colon).IndexOfAny(' ', '\t') >= 0' -> '> 0' survived. A field line such as ' X: y' (obs-fold carrying a colon) then parses as a field named ' X' instead of failing MalformedHeaderField, so the transfer ends 0 instead of failing. SendAsync_MalformedField_FailsMalformedHeaderField's rows ' folded' and '\tfolded' have no colon and are refused by the colon <= 0 clause first, so whitespace at index 0 before a colon is never tested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.AzureStorage.UnitLibrary/Http/AzureStorageHttpResponseHead.cs:31:>= -Member ParseField -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-25: filed proposed.
