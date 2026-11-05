---
id: AF-0666
title: Surviving mutant: the vsock/Hyper-V stream can stop owning its socket, so closing the connection never closes the socket
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:OpenAsync-true:surviving-mutant
reproduction: mutation Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:27:true
task: BL-4854
tasks: BL-4854
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed: 2026-11-05
closed-how: mechanical
closed-by: 2026-11-05_0819.md
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

- 2026-11-05 | 2026-11-05_0819.md | reproduces: no | Ran the -Site command: 'return new NetworkStream(socket, ownsSocket: true)' -> 'ownsSocket: false' at line 27, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
