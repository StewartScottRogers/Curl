---
id: BL-4638
title: Fix AF-0532: Surviving mutant: ConnectionCache.CloseIdleToMakeRoomAsync can be off by two on the connection limit without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/ConnectionCacheTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-02
---
# BL-4638 — Fix AF-0532: Surviving mutant: ConnectionCache.CloseIdleToMakeRoomAsync can be off by two on the connection limit without a test failing

## Goal

The defect the audit office reported as AF-0532 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0532 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0532-surviving-mutant-connectioncache-closeidletomakero.md`.

Location: `Curl.Networking.UnitLibrary/ConnectionCache.cs:123`

Location: `Curl.Networking.UnitLibrary/ConnectionCache.cs:123`

Mutant 'Idle.Count + Leased.Count - Math.Max(limit - 1, 0)' -> 'Math.Max(limit + 1, 0)' survived (Curl.Networking.UnitTests green). The method enforces CURLMOPT_MAX_TOTAL_CONNECTIONS (CGR-150) by closing idle connections to make room. With the mutant, two more connections than the limit stay open before any idle one is closed, which changes how many sockets are open and which connections are reused or closed (the verbose closing lines).

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ConnectionCache.cs:123:-1 -Member CloseIdleToMakeRoomAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `Curl.Networking.UnitTests/ConnectionCacheTests.cs` to `touches`: the mutant survives because no Networking test calls `CloseIdleToMakeRoomAsync` (only `Curl.Engine.UnitTests/Cgr150Tests.cs` did), and the reproduction runs `Curl.Networking.UnitTests`. No task in Doing on `origin/work/dark-factory` names a Networking path.
- Fix: two tests in `ConnectionCacheTests` - `CloseIdleToMakeRoomAsync_ThreeIdleAndOneLeasedForANewHost_ClosesTheOldestIdleUntilFewerThanTheLimitRemain` (limits 1 to 5, closing 3, 3, 2, 1, 0 of the three idle connections, oldest first, never the leased one) and `CloseIdleToMakeRoomAsync_AnIdleConnectionToTheSameHostWaits_ClosesNone` (the reusable branch, host compared ignoring case). Production code is unchanged, so no quality run was needed.
- Reproduction: a lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant (`Math.Max(limit - 1, 0)` -> `Math.Max(limit - -1, 0)` at ConnectionCache.cs:123) was applied by hand: 4 of the 5 data rows failed (killed); reverted, all 16 ConnectionCacheTests pass.
- Fast tests: one unrelated failure, `Amqp1SendCases.Credit_ContainerGrantsNoneFor300ms_CurlWaitsThenSends`, passed when run alone; filed as BL-4655. The new tests use no file system, path or OS text, so they are platform-neutral.

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-02: Doing -> Done. ConnectionCacheTests pins CloseIdleToMakeRoomAsync's limit, killing AF-0532's off-by-two mutant
