---
id: AF-0634
title: Surviving mutant: an scp or sftp transfer can report its known-hosts file missing while one exists, and no test fails
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:ResolveSshOptionsAsync-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:287:&&
task: BL-4813
tasks: BL-4813
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0634 - Surviving mutant: an scp or sftp transfer can report its known-hosts file missing while one exists, and no test fails

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:287`: Surviving mutant: an scp or sftp transfer can report its known-hosts file missing while one exists, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:287`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'bool missing = knownHosts is null && !settings.Insecure;' -> '||'. Without -k, every scp or sftp transfer counts as missing its known-hosts file even when --knownhosts or the default search found one. ReportKnownHostsFileMissingAsync then writes the missing-file warning, or fails the transfer when a fingerprint is not given. That changes stderr and the exit code. ResolveSshOptionsAsync_NoKnownHostsFileWithFingerprint_WarnsAndGoesOn covers only the file-really-missing case.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:287:&& -Member ResolveSshOptionsAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
