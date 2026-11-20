---
id: AF-0898
title: ApplyTo_NoVersionOptionOffWindows_UsesAnH2Entry checks only that some route exists
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs:ApplyTo_NoVersionOptionOffWindows_UsesAnH2Entry:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0898 - ApplyTo_NoVersionOptionOffWindows_UsesAnH2Entry checks only that some route exists

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs:45`: ApplyTo_NoVersionOptionOffWindows_UsesAnH2Entry checks only that some route exists.

## Evidence

Location: `Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs:45`

The name promises the transfer uses the stored h2 entry for port 8443. The only assertion is Assert.IsNotNull(cache.ApplyTo(CurlUrl.Parse("https://h.test/"), new HttpRequestOptions()).AltSvcRoute); A route with the wrong ALPN, host or port passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/AltSvcTransferCacheH3SwitchTests.cs -Pattern 'Assert.IsNotNull\(cache.ApplyTo'
```

- Expected: No match: the test asserts the route's ALPN h2 and port 8443.
- Actual: AltSvcTransferCacheH3SwitchTests.cs:45: Assert.IsNotNull(cache.ApplyTo(...).AltSvcRoute);

## Re-audits

## Log

- 2026-11-19: filed proposed.
