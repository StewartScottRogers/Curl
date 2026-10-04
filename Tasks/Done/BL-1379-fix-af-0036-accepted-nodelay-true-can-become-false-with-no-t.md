---
id: BL-1379
title: Fix AF-0036: `accepted.NoDelay = true` can become false with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1379 — Fix AF-0036: `accepted.NoDelay = true` can become false with no test failing

## Goal

The defect the audit office reported as AF-0036 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0036 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0036-accepted-nodelay-true-can-become-false-with-no-tes.md`.

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86`

Location: `Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86`

Mutant `accepted.NoDelay = false` survived at seed 0. No test checks that the accepted socket has Nagle disabled, so the socket option can regress silently.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at TcpPendingConnection.cs:86 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TcpPendingConnection.cs:86 true

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- AF-0036 reports the same mutant as AF-0030, which BL-1372 (commit 0a6cb201) already fixed:
  it extracted `TcpPendingConnection.TurnOffNagle` (now line 77, the finding's line 86 before the
  extraction) and pinned it with `TcpPendingConnectionTests.TurnOffNagle_SetsNoDelayOnTheAcceptedSocket`.
  No code change was needed.
- Verified by hand, since a lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (the guard
  refuses audit paths): with the line changed to `accepted.NoDelay = false`, that test fails
  (1 failed, 3 passed of `TcpPendingConnectionTests`); the mutant was then reverted. The re-audit
  by the quality auditor confirms it with the reproduction itself.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: all 33 test assemblies green.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. AF-0036's NoDelay mutant is killed by BL-1372's TurnOffNagle test
