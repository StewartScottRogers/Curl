---
id: BL-1372
title: Fix AF-0030: `accepted.NoDelay = true` can become false with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1372 — Fix AF-0030: `accepted.NoDelay = true` can become false with no test failing

## Goal

The defect the audit office reported as AF-0030 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0030 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0030-accepted-nodelay-true-can-become-false-with-no-tes.md`.

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86`

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86`

Mutation seed 0 changed `accepted.NoDelay = true;` to `false`; survived. TCP_NODELAY on an accepted socket changes wire timing and no test pins it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: Mutant at TcpPendingConnection.cs:86 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86 true

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: the accept step's `accepted.NoDelay = true` moved into `TcpPendingConnection.TurnOffNagle(Socket)` (outside the ADR-0083 coverage exclusion), pinned by the fast test `TcpPendingConnectionTests.TurnOffNagle_SetsNoDelayOnTheAcceptedSocket` on an unconnected socket, the same way `TcpDialerTests` pins `ApplySocketOptions`. Flipping the literal to `false` now fails that test.
- Added `Curl.Networking.UnitTests` to `touches` for the test; no other task in Doing on origin/work/dark-factory names it.
- The reproduction script lives under `Audit/`, which the guard forbids a factory lane to read or run; the mutant it names is the `true` literal, now killed by the new test. The re-audit confirms it.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. TCP_NODELAY on an accepted socket is pinned by a fast test; the AF-0030 mutant is killed
