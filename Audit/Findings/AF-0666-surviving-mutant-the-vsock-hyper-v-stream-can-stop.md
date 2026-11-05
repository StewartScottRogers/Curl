---
id: AF-0666
title: Surviving mutant: the vsock/Hyper-V stream can stop owning its socket, so closing the connection never closes the socket
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:OpenAsync-true:surviving-mutant
reproduction: mutation Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:27:true
task: none
tasks:
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0666 - Surviving mutant: the vsock/Hyper-V stream can stop owning its socket, so closing the connection never closes the socket

## Summary

Medium finding from the quality auditor at `Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:27`: Surviving mutant: the vsock/Hyper-V stream can stop owning its socket, so closing the connection never closes the socket. Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:27`

Mutant 'return new NetworkStream(socket, ownsSocket: true);' -> 'ownsSocket: false' survived (sample -Library Curl.Extension.VirtualMachineSocket.UnitLibrary -MaxMutants 25 -Seed 413592). With it, disposing the connection leaks the socket and the peer never sees the close. The class is [ExcludeFromCodeCoverage] because it needs a live vsock or Hyper-V peer, so no test reaches the ownership decision.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:27:true -Member OpenAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
