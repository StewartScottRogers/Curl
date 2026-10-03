---
id: BL-1371
title: Fix AF-0029: CRL expiry check `list.NextUpdate < now` can become `<=` with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1371 — Fix AF-0029: CRL expiry check `list.NextUpdate < now` can become `<=` with no test failing

## Goal

The defect the audit office reported as AF-0029 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0029 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0029-crl-expiry-check-list-nextupdate-now-can-become-wi.md`.

Location: `Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116`

Location: `Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116`

Invoke-MutationTest (seed 0) mutated `return list.NextUpdate < now ? OpenSslVerifyResult.CertificateRevocationListHasExpired : null;` to `<=`; outcome survived. The boundary decides whether a CRL is reported as expired, a refusal a user can see.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: Mutant at CertificateRevocationListFile.cs:116 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116 <

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
