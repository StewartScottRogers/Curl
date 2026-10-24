---
id: AF-0422
title: StreamConnection.DiscardUnreadAndShutDownSocketUnder's Socket.Available > 0 can become >= 0 with no test failing, so closing a connection blocks in Receive
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/StreamConnection.cs:DiscardUnreadAndShutDownSocketUnder-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/StreamConnection.cs:156:>
task: none
tasks:
found: 2026-10-24
found-at: d3ee244c64c5cb56d3fe0ca2ed9728e9383eb8ea
scorecard: 2026-10-24_0845.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0422 - StreamConnection.DiscardUnreadAndShutDownSocketUnder's Socket.Available > 0 can become >= 0 with no test failing, so closing a connection blocks in Receive

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/StreamConnection.cs:156`: StreamConnection.DiscardUnreadAndShutDownSocketUnder's Socket.Available > 0 can become >= 0 with no test failing, so closing a connection blocks in Receive.

## Evidence

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:156`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: survived, 'while (network.Socket.Available > 0 && network.Socket.Receive(discard) > 0)' -> '>= 0'. With >= 0, the loop calls the blocking Socket.Receive even when nothing is waiting. Shutting down a connection whose peer keeps it open then waits until the peer sends or closes, which hangs curl at the end of a transfer. No test covers the drain on a real NetworkStream.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/StreamConnection.cs:156:> -Member DiscardUnreadAndShutDownSocketUnder -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-24: filed proposed.
