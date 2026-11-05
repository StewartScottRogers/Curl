---
id: BL-4854
title: Fix AF-0666: Surviving mutant: the vsock/Hyper-V stream can stop owning its socket, so closing the connection never closes the socket
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.VirtualMachineSocket.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4854 — Fix AF-0666: Surviving mutant: the vsock/Hyper-V stream can stop owning its socket, so closing the connection never closes the socket

## Goal

The defect the audit office reported as AF-0666 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0666 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0666-surviving-mutant-the-vsock-hyper-v-stream-can-stop.md`.

Location: `Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:27`

Location: `Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:27`

Mutant 'return new NetworkStream(socket, ownsSocket: true);' -> 'ownsSocket: false' survived (sample -Library Curl.Extension.VirtualMachineSocket.UnitLibrary -MaxMutants 25 -Seed 413592). With it, disposing the connection leaks the socket and the peer never sees the close. The class is [ExcludeFromCodeCoverage] because it needs a live vsock or Hyper-V peer, so no test reaches the ownership decision.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.VirtualMachineSocket.UnitLibrary/SystemVirtualMachineSocketOpener.cs:27:true -Member OpenAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
