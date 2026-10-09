---
id: BL-1867
title: Fix AF-0117: RevocationCheckIncomplete '!options.Insecure && RevocationBestEffortTolerates(chain)' can become '||' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1867 — Fix AF-0117: RevocationCheckIncomplete '!options.Insecure && RevocationBestEffortTolerates(chain)' can become '||' with no test failing

## Goal

The defect the audit office reported as AF-0117 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0117 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0117-revocationcheckincomplete-options-insecure-revocat.md`.

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:129`

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:129`

Mutant '!options.Insecure && RevocationBestEffortTolerates(chain)' -> '!options.Insecure || ...' survived (the tool names the member CaCertificateFileUnusable; the line is in Judge). With the mutant every verified (non-insecure) handshake is marked RevocationCheckIncomplete, so SslStreamTlsProvider.ReportRevocationCheckIncomplete (line 646) reports it, and TcpConnector (line 2243) writes a revocation-check-incomplete warning to the diagnostic log for every TLS connection, not only for --ssl-revoke-best-effort with offline revocation status. curl's own output is unchanged, so this is a decision rather than output bytes. No test asserts the flag is false on a normal verified handshake.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:129:&& -Member CaCertificateFileUnusable -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-09: Created.
