---
id: AF-0811
title: Surviving mutant: the curl tool's and multi handles' transfers can take ownership of their connections and close them at handle cleanup
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:OwnTheConnectionsOpened-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:337:&&
task: BL-5265
tasks: BL-5265
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0811 - Surviving mutant: the curl tool's and multi handles' transfers can take ownership of their connections and close them at handle cleanup

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:337`: Surviving mutant: the curl tool's and multi handles' transfers can take ownership of their connections and close them at handle cleanup.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:337`

The mutant changes `ToolSettings is null && !shared.MultiOwners.ContainsKey(this)` to `||` and survived the Curl.Engine.UnitTests run (seed 213433). With it, every curl-tool transfer and every multi-owned easy handle makes its opened connections its own, so they are closed when the handle is disposed. Connection reuse across a tool run's URLs ('Re-using existing connection'), FTP QUIT timing and the request bytes on later transfers would change, and no test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:337:&& -Member OwnTheConnectionsOpened -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
