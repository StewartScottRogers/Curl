---
id: BL-3124
title: Fix AF-0344: OpenSslCipherSuites.Find's ignoreCase: false can become true with no test failing, so cipher names in the wrong case are accepted
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
---
# BL-3124 — Fix AF-0344: OpenSslCipherSuites.Find's ignoreCase: false can become true with no test failing, so cipher names in the wrong case are accepted

## Goal

The defect the audit office reported as AF-0344 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0344 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0344-opensslciphersuites-find-s-ignorecase-false-can-be.md`.

Location: `Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs:156`

Location: `Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs:156`

Sampled mutant (seed 0) survived: 'Enum.TryParse<TlsCipherSuite>(entry, ignoreCase: false, ...)' became 'ignoreCase: true'. An IANA cipher name such as TLS_aes_128_gcm_sha256 then matches a suite instead of being unknown. A --ciphers / --tls13-ciphers list the OpenSSL build refuses (exit 59) would be accepted. No test pins the case-sensitive match.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs:156:false -Member Find -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
