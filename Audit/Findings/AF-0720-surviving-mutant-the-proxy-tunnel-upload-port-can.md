---
id: AF-0720
title: Surviving mutant: the proxy-tunnel upload port can send an empty buffer when it is exactly the data's size
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitLibrary/LibProxyTunnelUpload.cs:Read-ge:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/LibProxyTunnelUpload.cs:131:>=
task: BL-5079
tasks: BL-5079
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0720 - Surviving mutant: the proxy-tunnel upload port can send an empty buffer when it is exactly the data's size

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/LibProxyTunnelUpload.cs:131`: Surviving mutant: the proxy-tunnel upload port can send an empty buffer when it is exactly the data's size. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/LibProxyTunnelUpload.cs:131`

Mutant 'buffer.Length >= Data.Length' -> '>' survived. For a buffer exactly Data.Length long the mutant copies nothing yet still returns Data.Length, so the upstream case would upload zero bytes in place of the body. The exact-size read is untested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/LibProxyTunnelUpload.cs:131:>= -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
