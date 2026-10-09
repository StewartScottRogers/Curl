---
id: BL-1867
title: Fix AF-0117: RevocationCheckIncomplete '!options.Insecure && RevocationBestEffortTolerates(chain)' can become '||' with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `AuthenticateAsClientAsync_WhenVerifiedWithoutRevocationCheckBestEffort_DoesNotReportTheCheckIncomplete` (OpenSSL build, `--cacert`, no `--ssl-revoke-best-effort`, runs on every platform): a verified handshake must leave `RevocationCheckIncomplete` false. No production change; the code was right, only the test was missing.
- `touches` gained `Curl.Networking.UnitTests`, where the test lives; no other task in Doing on `origin/work/dark-factory` names it (BL-1865 touches `Curl.Protocol.Http.UnitLibrary`).
- The audit guard refuses a lane `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand instead: with `!options.Insecure || RevocationBestEffortTolerates(chain)` at `ServerCertificateVerification.cs:129` the new test fails ("Expected condition to be false"), and passes once reverted. The re-audit runs the tool itself.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Test pins RevocationCheckIncomplete false on a verified handshake; AF-0117 mutant killed
