---
id: BL-1701
title: Fix AF-0058: HandBuiltCertificateVerifier.LoadAll: 'when (loaded.Count > 0)' can become >= 0 with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1701 — Fix AF-0058: HandBuiltCertificateVerifier.LoadAll: 'when (loaded.Count > 0)' can become >= 0 with no test failing

## Goal

The defect the audit office reported as AF-0058 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0058 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0058-handbuiltcertificateverifier-loadall-when-loaded-c.md`.

Location: `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:82`

Location: `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:82`

Mutant: catch (CryptographicException) when (loaded.Count > 0) -> when (loaded.Count >= 0). It survived the sampled run (seed 0). The comment says the server's own certificate failing to parse means 'none, as though it sent nothing'. With the mutant, an unparseable leaf is skipped and the next chain candidate becomes the first certificate. That changes which certificate the hand-built TLS stack verifies the host against, a trust decision. No test sends an unparseable leaf followed by a valid certificate.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs:82:> -Member LoadAll -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
