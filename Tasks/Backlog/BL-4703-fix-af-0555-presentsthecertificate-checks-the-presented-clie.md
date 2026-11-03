---
id: BL-4703
title: Fix AF-0555: PresentsTheCertificate checks the presented client certificate only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4703 — Fix AF-0555: PresentsTheCertificate checks the presented client certificate only for null

## Goal

The defect the audit office reported as AF-0555 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0555 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0555-presentsthecertificate-checks-the-presented-client.md`.

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.TlsOptions.cs:67`

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.TlsOptions.cs:67`

'Assert.IsNotNull(opener.Opened.Single().Server!.Tls!.ClientCertificate);' is the only check of the certificate; the test never compares it with s_clientCertificate from both.pem.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorQuicTests.TlsOptions.cs -Pattern 'ClientCertificate\);'
```

- Expected: The presented certificate's DER is compared with the file's certificate.
- Actual: TcpConnectorQuicTests.TlsOptions.cs:67: Assert.IsNotNull(opener.Opened.Single().Server!.Tls!.ClientCertificate);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
