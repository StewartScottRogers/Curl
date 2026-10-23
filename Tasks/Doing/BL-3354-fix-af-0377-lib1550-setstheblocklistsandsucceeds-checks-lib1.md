---
id: BL-3354
title: Fix AF-0377: Lib1550_SetsTheBlocklistsAndSucceeds checks Lib1550's own constant lists, never that the blocklists were set on the multi handle
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-23
completed:
---
# BL-3354 — Fix AF-0377: Lib1550_SetsTheBlocklistsAndSucceeds checks Lib1550's own constant lists, never that the blocklists were set on the multi handle

## Goal

The defect the audit office reported as AF-0377 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0377 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0377-lib1550-setstheblocklistsandsucceeds-checks-lib155.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs:398`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs:398`

The test asserts result.ExitCode == 0, the program name, CollectionAssert.AreEqual(new[] { "curl.se:443", "example.com:80" }, Lib1550.BlockedSites.ToArray()) and Lib1550.BlockedServers.Count == 2. BlockedSites and BlockedServers are static constants in Lib1550.cs:14-17. What the port does is multi.SetOption(PipeliningServerBlocklist, ...) and multi.SetOption(PipeliningSiteBlocklist, ...) at Lib1550.cs:41-42, and no assertion looks at that: deleting both calls leaves the test passing. FakeMultiTransfer records MultiOptions (FakeMultiTransfer.cs:33), so engine.Multi.Options could be checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs -Pattern 'Lib1550_SetsTheBlocklistsAndSucceeds' -Context 0,10
```

- Expected: The body asserts the blocklists on engine.Multi.Options.
- Actual: The body asserts only Lib1550.BlockedSites and Lib1550.BlockedServers.Count, the program's own constants.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
