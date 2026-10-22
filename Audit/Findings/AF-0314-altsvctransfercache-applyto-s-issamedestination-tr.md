---
id: AF-0314
title: AltSvcTransferCache.ApplyTo's IsSameDestination: true can become false with no Curl.Engine.UnitTests test failing, so alt-svc entries to another host are no longer followed
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:ApplyTo-true:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:144:true
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0314 - AltSvcTransferCache.ApplyTo's IsSameDestination: true can become false with no Curl.Engine.UnitTests test failing, so alt-svc entries to another host are no longer followed

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:144`: AltSvcTransferCache.ApplyTo's IsSameDestination: true can become false with no Curl.Engine.UnitTests test failing, so alt-svc entries to another host are no longer followed.

## Evidence

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:144`

Sampled mutant (seed 0) survived: line 144 '{ IsSameDestination: true } same => http with { AltSvcRoute = null, Version = SameDestinationVersion(...), TcpFirstAttemptVersion = ... }' with true flipped to false. Under the mutant, an entry for another host or port takes the same-destination arm (AltSvcRoute = null, so the transfer never dials the alternative). A same-destination entry falls through to '{ } other' and gets a route to itself plus SwitchedVersion. This changes where the request goes, the HTTP version sent, and the 'Alt-svc connecting from' -v line, and no test in the twin noticed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:144:true -Member ApplyTo -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
