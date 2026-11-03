---
id: AF-0549
title: Lib1678 checks the shared session cache only for null
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:Lib1678_LibraryWithSslsExport_ImportsThePacket64TimesOverASharedSessionCache:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0549 - Lib1678 checks the shared session cache only for null

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:134`: Lib1678 checks the shared session cache only for null. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:134`

'Assert.IsNotNull(engine.LastShare);' is the only check of the 'shared session cache' the name promises; a share that shares nothing, or no SSL sessions, passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs -Pattern 'Assert.IsNotNull\(engine.LastShare\)' | Select-Object -First 1
```

- Expected: An assertion that the share shares SSL sessions.
- Actual: Lib1649To1906Tests.cs:134: Assert.IsNotNull(engine.LastShare);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
