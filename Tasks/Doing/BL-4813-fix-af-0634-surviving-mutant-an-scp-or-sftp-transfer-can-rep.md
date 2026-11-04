---
id: BL-4813
title: Fix AF-0634: Surviving mutant: an scp or sftp transfer can report its known-hosts file missing while one exists, and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4813 — Fix AF-0634: Surviving mutant: an scp or sftp transfer can report its known-hosts file missing while one exists, and no test fails

## Goal

The defect the audit office reported as AF-0634 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0634 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0634-surviving-mutant-an-scp-or-sftp-transfer-can-repor.md`.

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:287`

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:287`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'bool missing = knownHosts is null && !settings.Insecure;' -> '||'. Without -k, every scp or sftp transfer counts as missing its known-hosts file even when --knownhosts or the default search found one. ReportKnownHostsFileMissingAsync then writes the missing-file warning, or fails the transfer when a fingerprint is not given. That changes stderr and the exit code. ResolveSshOptionsAsync_NoKnownHostsFileWithFingerprint_WarnsAndGoesOn covers only the file-really-missing case.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:287:&& -Member ResolveSshOptionsAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
