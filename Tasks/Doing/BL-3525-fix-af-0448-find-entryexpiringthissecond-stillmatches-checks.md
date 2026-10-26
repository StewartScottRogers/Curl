---
id: BL-3525
title: Fix AF-0448: Find_EntryExpiringThisSecond_StillMatches checks only that some entry is found, not that it is the h2 :1 entry
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-25
completed:
---
# BL-3525 — Fix AF-0448: Find_EntryExpiringThisSecond_StillMatches checks only that some entry is found, not that it is the h2 :1 entry

## Goal

The defect the audit office reported as AF-0448 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0448 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0448-find-entryexpiringthissecond-stillmatches-checks-o.md`.

Location: `Curl.Core.UnitTests/AltSvc/AltSvcCacheTests.cs:344`

Location: `Curl.Core.UnitTests/AltSvc/AltSvcCacheTests.cs:344`

The only assertion is Assert.IsNotNull(entry) after Apply(cache, "h2=\":1\"; ma=10", ...) and a 10.9 s advance. A Find that returned a wrong entry (wrong ALPN, port or expiry) would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Core.UnitTests/AltSvc/AltSvcCacheTests.cs -Pattern 'Find_EntryExpiringThisSecond_StillMatches' -Context 0,14
```

- Expected: The test asserts the found entry's ALPN (h2) and port (1).
- Actual: The only assertion is Assert.IsNotNull(entry);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
