---
id: BL-4746
title: Fix AF-0582: WithNoSessionId_OffersNoSessionAndLeavesTheCacheAlone checks the kept session only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4746 — Fix AF-0582: WithNoSessionId_OffersNoSessionAndLeavesTheCacheAlone checks the kept session only for null

## Goal

The defect the audit office reported as AF-0582 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0582 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0582-withnosessionid-offersnosessionandleavesthecacheal.md`.

Location: `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SessionIdAndBeast.cs:40`

Location: `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SessionIdAndBeast.cs:40`

Step 0 weak-assertion. 'LeavesTheCacheAlone' says the cache still holds the original session; the only check is Assert.IsNotNull(keptSession, ...), so a handshake that replaced the cached session with a new one (which --no-sessionid forbids) passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SessionIdAndBeast.cs -Pattern 'Assert.IsNotNull\(keptSession'
```

- Expected: No match: the test compares the kept session with the one the cache was given.
- Actual: Assert.IsNotNull(keptSession, "--no-sessionid neither takes nor keeps a session");

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now checks the kept session is the cached one: its ticket and ticket age add match the session the cache was given, and a second Take finds nothing, so a handshake that replaced or added a session fails. Test-only change, so no quality run; platform-neutral (no paths, OS text or certificates). Curl.Networking.UnitTests fast tests: 4018 passed, 0 failed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. The no-sessionid test compares the kept session with the cached one
