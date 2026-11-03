---
id: AF-0556
title: Lib566 RunAsync_TransferFails_WritesNothing never checks that nothing was written
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:RunAsync_TransferFails_WritesNothing:name-lies
reproduction: none
task: BL-4704
tasks: BL-4704
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0556 - Lib566 RunAsync_TransferFails_WritesNothing never checks that nothing was written

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:404`: Lib566 RunAsync_TransferFails_WritesNothing never checks that nothing was written. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs:404`

The body's only assertion is 'Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);'. Neither run.OutputText nor any file Lib566 writes its Content-Length line to is checked, so a port that wrote output after the failure passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib554To566Tests.cs -Pattern 'RunAsync_TransferFails_WritesNothing' -Context 0,7
```

- Expected: An assertion that the output is empty.
- Actual: Only Assert.AreEqual((int)CurlCode.PartialFile, result.ExitCode);

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | RunAsync_TransferFails_WritesNothing asserts Assert.AreEqual(string.Empty, run.OutputText) and Assert.IsFalse(File.Exists(path)).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
