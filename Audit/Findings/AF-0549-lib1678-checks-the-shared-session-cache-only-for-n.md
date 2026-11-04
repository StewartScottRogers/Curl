---
id: AF-0549
title: Lib1678 checks the shared session cache only for null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:Lib1678_LibraryWithSslsExport_ImportsThePacket64TimesOverASharedSessionCache:weak-assertion
reproduction: none
task: BL-4697
tasks: BL-4697
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1017.md, 2026-11-03_1615.md
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

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | No match for Assert.IsNotNull(engine.LastShare) in Lib1649To1906Tests.cs; line 134 asserts CollectionAssert.AreEqual(new[] { CurlLockData.SslSession }, engine.LastShare!.Shared).
- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | No match; Lib1649To1906Tests.cs:134 asserts CollectionAssert.AreEqual(new[] { CurlLockData.SslSession }, engine.LastShare!.Shared).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
