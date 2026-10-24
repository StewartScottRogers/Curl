---
id: AF-0377
title: Lib1550_SetsTheBlocklistsAndSucceeds checks Lib1550's own constant lists, never that the blocklists were set on the multi handle
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs:Lib1550_SetsTheBlocklistsAndSucceeds:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0377 - Lib1550_SetsTheBlocklistsAndSucceeds checks Lib1550's own constant lists, never that the blocklists were set on the multi handle

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs:398`: Lib1550_SetsTheBlocklistsAndSucceeds checks Lib1550's own constant lists, never that the blocklists were set on the multi handle.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs:398`

The test asserts result.ExitCode == 0, the program name, CollectionAssert.AreEqual(new[] { "curl.se:443", "example.com:80" }, Lib1550.BlockedSites.ToArray()) and Lib1550.BlockedServers.Count == 2. BlockedSites and BlockedServers are static constants in Lib1550.cs:14-17. What the port does is multi.SetOption(PipeliningServerBlocklist, ...) and multi.SetOption(PipeliningSiteBlocklist, ...) at Lib1550.cs:41-42, and no assertion looks at that: deleting both calls leaves the test passing. FakeMultiTransfer records MultiOptions (FakeMultiTransfer.cs:33), so engine.Multi.Options could be checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1535To1550Tests.cs -Pattern 'Lib1550_SetsTheBlocklistsAndSucceeds' -Context 0,10
```

- Expected: The body asserts the blocklists on engine.Multi.Options.
- Actual: The body asserts only Lib1550.BlockedSites and Lib1550.BlockedServers.Count, the program's own constants.

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | reproduces: no | Ran the Select-String reproduction: Lib1550_SetsTheBlocklistsAndSucceeds (Lib1535To1550Tests.cs:399) now asserts CollectionAssert.AreEqual(new[] { new MultiOptions(), new MultiOptions() }, engine.Multi.OptionsWritten) and Assert.AreEqual(new MultiOptions(), engine.Multi.Options) - it checks the multi handle's options were written for both blocklists, not Lib1550's own lists.

## Log

- 2026-10-23: filed proposed.
