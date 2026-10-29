---
id: AF-0458
title: TcpPendingConnection's accepted connection can stop owning its socket (ownsSocket: true -> false) with no test failing, so an accepted data connection is never closed
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TcpPendingConnection.cs:NetworkStream-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpPendingConnection.cs:99:true
task: BL-4181
tasks: BL-4181
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0458 - TcpPendingConnection's accepted connection can stop owning its socket (ownsSocket: true -> false) with no test failing, so an accepted data connection is never closed

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:99`: TcpPendingConnection's accepted connection can stop owning its socket (ownsSocket: true -> false) with no test failing, so an accepted data connection is never closed. Reported by an auditor flagged unreliable in 2026-10-29_0606.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:99`

Line 99: `new NetworkStream(accepted, ownsSocket: true),` in AcceptStreamConnectionAsync. With ownsSocket false, disposing the StreamConnection leaves the accepted socket open. The peer of an active-mode (PORT/EPRT) data connection then never sees a FIN, so an upload's end is not signalled and the socket leaks. The mutation run (seed 0) left it surviving. The method is [ExcludeFromCodeCoverage] (`measured by the Integration run`), so no fast test reaches it.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpPendingConnection.cs:99:true -Member NetworkStream -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a loopback accept test sees the accepted socket closed when the connection is disposed.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
