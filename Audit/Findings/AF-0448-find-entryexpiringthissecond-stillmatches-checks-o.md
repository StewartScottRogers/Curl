---
id: AF-0448
title: Find_EntryExpiringThisSecond_StillMatches checks only that some entry is found, not that it is the h2 :1 entry
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
key: quality:Curl.Core.UnitTests/AltSvc/AltSvcCacheTests.cs:Find_EntryExpiringThisSecond_StillMatches:weak-assertion
reproduction: none
task: BL-3525
tasks: BL-3525
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed: 2026-10-29
closed-how: consecutive
closed-by: 2026-10-25_1944.md, 2026-10-29_0606.md
---
# AF-0448 - Find_EntryExpiringThisSecond_StillMatches checks only that some entry is found, not that it is the h2 :1 entry

## Summary

Low finding from the quality auditor at `Curl.Core.UnitTests/AltSvc/AltSvcCacheTests.cs:344`: Find_EntryExpiringThisSecond_StillMatches checks only that some entry is found, not that it is the h2 :1 entry. Reported by an auditor flagged unreliable in 2026-10-25_1542.md.

## Evidence

Location: `Curl.Core.UnitTests/AltSvc/AltSvcCacheTests.cs:344`

The only assertion is Assert.IsNotNull(entry) after Apply(cache, "h2=\":1\"; ma=10", ...) and a 10.9 s advance. A Find that returned a wrong entry (wrong ALPN, port or expiry) would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitTests/AltSvc/AltSvcCacheTests.cs -Pattern 'Find_EntryExpiringThisSecond_StillMatches' -Context 0,14
```

- Expected: The test asserts the found entry's ALPN (h2) and port (1).
- Actual: The only assertion is Assert.IsNotNull(entry);

## Re-audits

- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | AltSvcCacheTests.cs:348-352: after IsNotNull(entry) it asserts SourceHost 'a.example', DestinationAlpn H2, DestinationPort 1 and Expires 2026-09-29 05:21:56Z, so it checks for the h2 :1 entry exactly.
- 2026-10-29 | 2026-10-29_0606.md | reproduces: no | Ran the Select-String reproduction. Find_EntryExpiringThisSecond_StillMatches (AltSvcCacheTests.cs:331) now asserts SourceHost a.example, DestinationAlpn H2, DestinationPort 1 and Expires 2026-09-29 05:21:56Z (lines 349-353), not only that some entry was found.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-29: accepted -> closed. Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
