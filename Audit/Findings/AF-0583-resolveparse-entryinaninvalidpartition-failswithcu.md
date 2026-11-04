---
id: AF-0583
title: ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_2203.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1615.md, 2026-11-03_2203.md).
key: quality:Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text:name-lies
reproduction: none
task: BL-4747
tasks: BL-4747
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1615.md, 2026-11-03_2203.md
---
# AF-0583 - ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:460`: ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text never checks exit 49. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Networking.UnitTests/NetworkingAdversarialTests.cs:460`

Step 0 name-lies. ResolveOverrides exposes the exit code (ParseFailureCodeWith, ResolveOverrides.cs:71, SetoptOptionSyntax = 49) but the test asserts only overrides.ParseError's text and the entry count.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/NetworkingAdversarialTests.cs -Pattern 'ResolveParse_EntryInAnInvalidPartition_FailsWithCurlsExit49Text' -Context 0,7
```

- Expected: The body asserts CurlExitCode.SetoptOptionSyntax.
- Actual: Only ParseError, Entries and Find are asserted.

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Asserts Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, overrides.ParseFailureCodeWith(...)) (49).
- 2026-11-03 | 2026-11-03_2203.md | reproduces: no | Ran the reproduction: line 467 Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, ...); SetoptOptionSyntax is 49.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_2203.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1615.md, 2026-11-03_2203.md).
