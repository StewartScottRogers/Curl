---
id: BL-1700
title: Fix AF-0057: ClientCertificateLoader: the OpenSSL build's recognisesDriveLetters: false can become true with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-07
---
# BL-1700 — Fix AF-0057: ClientCertificateLoader: the OpenSSL build's recognisesDriveLetters: false can become true with no test failing

## Goal

The defect the audit office reported as AF-0057 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0057 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0057-clientcertificateloader-the-openssl-build-s-recogn.md`.

Location: `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:74`

Location: `Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:74`

Mutant: return LoadAsOpenSslBuild(options, recognisesDriveLetters: false); -> recognisesDriveLetters: true. It survived the sampled run (seed 0). This flag decides how --cert splits 'file:passphrase'. On the OpenSSL build a value like 'C:cert.pem' or 'x:pass' would be split differently: a different file is loaded, or the passphrase is lost. That decides whether the transfer fails with exit 58. No test pins how the OpenSSL build splits a drive-letter-like --cert.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ClientCertificateLoader.cs:74:false -Member static -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A test pins how the OpenSSL build splits a drive-letter-like --cert, killing AF-0057's mutant
