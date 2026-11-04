---
id: AF-0656
title: Surviving mutant: a CONNECT reply head whose remaining line starts with a bare LF can be written as one -v line
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:ReportReplyHead-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165:<
task: none
tasks:
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0656 - Surviving mutant: a CONNECT reply head whose remaining line starts with a bare LF can be written as one -v line

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165`: Surviving mutant: a CONNECT reply head whose remaining line starts with a bare LF can be written as one -v line.

## Evidence

Location: `Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165`

Mutant 'lineFeed < 0' -> 'lineFeed <= 0' survived. When the rest of the head starts with an LF, the mutant writes all of it as one line instead of splitting it, which changes the verbose output bytes. No test covers an LF at index 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ConnectTunnelVerboseLines.cs:165:< -Member ReportReplyHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
