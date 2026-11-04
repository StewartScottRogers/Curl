---
id: AF-0582
title: WithNoSessionId_OffersNoSessionAndLeavesTheCacheAlone checks the kept session only for null
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SessionIdAndBeast.cs:AuthenticateAsClientAsync_WithNoSessionId_OffersNoSessionAndLeavesTheCacheAlone:weak-assertion
reproduction: none
task: BL-4746
tasks: BL-4746
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0582 - WithNoSessionId_OffersNoSessionAndLeavesTheCacheAlone checks the kept session only for null

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SessionIdAndBeast.cs:40`: WithNoSessionId_OffersNoSessionAndLeavesTheCacheAlone checks the kept session only for null. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SessionIdAndBeast.cs:40`

Step 0 weak-assertion. 'LeavesTheCacheAlone' says the cache still holds the original session; the only check is Assert.IsNotNull(keptSession, ...), so a handshake that replaced the cached session with a new one (which --no-sessionid forbids) passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/HandBuiltTlsProviderTests.SessionIdAndBeast.cs -Pattern 'Assert.IsNotNull\(keptSession'
```

- Expected: No match: the test compares the kept session with the one the cache was given.
- Actual: Assert.IsNotNull(keptSession, "--no-sessionid neither takes nor keeps a session");

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | No 'Assert.IsNotNull(keptSession' match; the test compares keptSession?.Ticket with CachedTicket and its TicketAgeAdd.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
