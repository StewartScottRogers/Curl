---
id: BL-1380
title: Fix AF-0037: Certificate name lookup `forIssuer: false` can become true with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1380 — Fix AF-0037: Certificate name lookup `forIssuer: false` can become true with no test failing

## Goal

The defect the audit office reported as AF-0037 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0037 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0037-certificate-name-lookup-forissuer-false-can-become.md`.

Location: `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`

Location: `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`

Mutant `GetNameInfo(SimpleName, forIssuer: true)` survived at seed 0. The subject name feeds a TLS failure message, so the mutant would print the issuer's name where the subject's belongs. No test tells the two apart.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: The mutant at TlsFailureMessages.cs:320 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320 false

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- AF-0037 reports the same mutant as AF-0031, which BL-1373 (commit 98622b66) already fixed
  with `OpenSslPeerFailedVerification_WithANameMismatchOnAnIssuedCertificate_NamesTheSubjectNotTheIssuer`:
  a leaf issued by `CN=Test Authority`, so subject and issuer differ. No code change was needed.
- Verified by hand, since a lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (the guard
  refuses audit paths): with line 320 changed to `forIssuer: true`, that test fails
  (1 failed, 16 passed of the `OpenSslPeerFailedVerification` tests); the mutant was then reverted.
  The re-audit by the quality auditor confirms it with the reproduction itself.
- `dotnet build`: 0 warnings, 0 errors. Fast tests: all green (Curl.Networking.UnitTests 2976 passed, 28 skipped).

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. The forIssuer mutant at TlsFailureMessages.cs:320 is killed by BL-1373's issued-certificate test
