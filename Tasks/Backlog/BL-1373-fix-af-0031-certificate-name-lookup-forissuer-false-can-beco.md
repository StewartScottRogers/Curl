---
id: BL-1373
title: Fix AF-0031: Certificate name lookup `forIssuer: false` can become true with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-03
completed:
---
# BL-1373 — Fix AF-0031: Certificate name lookup `forIssuer: false` can become true with no test failing

## Goal

The defect the audit office reported as AF-0031 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0031 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0031-certificate-name-lookup-forissuer-false-can-become.md`.

Location: `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`

Location: `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`

Mutation seed 0 changed `certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false)` to `forIssuer: true`; survived. The error text would name the issuer instead of the subject, and no test notices.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: Mutant at TlsFailureMessages.cs:320 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320 false

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-03: Created.
