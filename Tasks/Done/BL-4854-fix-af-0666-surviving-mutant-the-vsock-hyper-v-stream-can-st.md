---
id: BL-4854
title: Fix AF-0666: Surviving mutant: the vsock/Hyper-V stream can stop owning its socket, so closing the connection never closes the socket
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.VirtualMachineSocket.UnitLibrary, Curl.Extension.VirtualMachineSocket.UnitTests/SystemVirtualMachineSocketOpenerTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: `SystemVirtualMachineSocketOpener` takes its socket creation and blocking connect as delegates through its
  primary constructor; the public parameterless constructor gives the real AF_VSOCK/AF_HYPERV ones, which alone keep
  `[ExcludeFromCodeCoverage]` (ADR-0083). The class itself is now covered: `SystemVirtualMachineSocketOpenerTests`
  gives it a loopback TCP socket and checks that disposing the stream closes the socket, and that a failed or
  cancelled connect closes it and throws. `return new NetworkStream(socket, ownsSocket: true);` stays on line 27 of
  `OpenAsync`, so the finding's site is unchanged. No ADR: this changes a seam, not behaviour.
- Reproduction: the audit guard refuses a lane running `Audit/Tools/Invoke-MutationTest.ps1`, so I made the mutant by
  hand instead: with line 27 changed to `ownsSocket: false`, `OpenAsync_StreamDisposed_ClosesTheSocket` failed (1 of
  55); I then changed it back. The re-audit runs the tool itself.
- The loopback test only connects and closes, without sending a byte, so it is a unit test under the root rules and
  works the same on Windows, Linux and macOS.
- Added `Curl.Extension.VirtualMachineSocket.UnitTests/SystemVirtualMachineSocketOpenerTests.cs` to touches for the
  test; no task in Doing on origin/work/dark-factory names that project.
- Measure-CodeQuality.ps1 for the library: 0 failing members. Build clean; fast tests green.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Opener tested through a loopback socket; ownsSocket mutant killed
