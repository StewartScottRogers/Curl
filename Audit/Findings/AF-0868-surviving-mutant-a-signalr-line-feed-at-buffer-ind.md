---
id: AF-0868
title: Surviving mutant: a SignalR line feed at buffer index 0 is not seen as a line end
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:ReadLineAsync-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:26:<
task: BL-5510
tasks: BL-5510
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0868 - Surviving mutant: a SignalR line feed at buffer index 0 is not seen as a line end

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:26`: Surviving mutant: a SignalR line feed at buffer index 0 is not seen as a line end. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:26`

Mutant 'while ((lineFeed = Array.IndexOf(buffer, (byte)'\n', start, end - start)) < 0)' -> '<= 0' survived. A line whose LF is the buffer's first byte (an empty line read at offset 0) would make ReadLineAsync keep filling: a stall or misparse of the head. No test reads a line ending at index 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.SignalR.UnitLibrary/Transport/BufferedConnectionReader.cs:26:< -Member ReadLineAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-18: filed proposed.
