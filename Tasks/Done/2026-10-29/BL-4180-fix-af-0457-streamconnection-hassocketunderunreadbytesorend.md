---
id: BL-4180
title: Fix AF-0457: StreamConnection.HasSocketUnderUnreadBytesOrEnd's || can become && with no test failing, so the check before a write misses a peer that already sent bytes or its FIN
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/StreamConnectionUnreadEndTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4180 — Fix AF-0457: StreamConnection.HasSocketUnderUnreadBytesOrEnd's || can become && with no test failing, so the check before a write misses a peer that already sent bytes or its FIN

## Goal

The defect the audit office reported as AF-0457 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0457 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0457-streamconnection-hassocketunderunreadbytesorend-s.md`.

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:77`

Location: `Curl.Networking.UnitLibrary/StreamConnection.cs:77`

Line 77: `stream is NetworkStream network && (network.Socket.Available > 0 || HasUnreadEnd(network.Socket));`. This is the production check run before each write (line 60, HasUnreadBytesOrEndBeforeWrite). With && it is true only when bytes are waiting and the peer's FIN is also pending, so it almost never fires. A reused connection the server has already closed, or answered early, would then be written to as if live. The mutation run (seed 0) left it surviving. The method is [ExcludeFromCodeCoverage] (`a thin socket adapter, measured by the Integration run`), and the fast tests replace it through the init property, so no unit test ever runs it.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/StreamConnection.cs:77:|| -Member HasSocketUnderUnreadBytesOrEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a loopback test whose peer sends its FIN before a write sees the end reported.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Networking.UnitTests/StreamConnectionUnreadEndTests.cs` to `touches`: the fix is a test, and no task in Doing on work/dark-factory names that file. The library itself is unchanged.
- Fix: two loopback tests call the production `HasUnreadBytesOrEndBeforeWrite` (the unreplaced default, `HasSocketUnderUnreadBytesOrEnd`) on a real `NetworkStream`. One has the peer shut down its send side (FIN, no byte) and waits for the socket to become readable before asking, so the answer is true. The other has nothing waiting, so the answer is false. They are unit tests, like `StreamConnectionShutDownSendingTests`: a loopback socket that carries no byte. They are platform-neutral, with no path, OS text or certificate.
- Reproduction: the audit guard refuses a lane running `Audit/Tools/Invoke-MutationTest.ps1`, so the same mutant (line 77 `||` to `&&`) was applied by hand. `HasUnreadBytesOrEndBeforeWrite_OverASocketWhosePeerSentItsFin_IsTrue` failed, so the mutant is killed. The mutant was then reverted. The quality auditor's re-audit runs the script itself.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: 51 test assemblies passed with 0 failures (Curl.Networking.UnitTests 3964 passed, 33 skipped). No production code changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-29: Doing -> Done. A loopback unit test of the pre-write check now kills the || to && mutant of StreamConnection.HasSocketUnderUnreadBytesOrEnd (AF-0457)
