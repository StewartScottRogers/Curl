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
completed: 2026-11-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The Windows-build test now compares the presented certificate's DER with s_clientCertificate (CollectionAssert.AreEqual), as its sibling test does; the reproduction's Select-String finds no match. Test-only change, so no quality run. Curl.Networking.UnitTests: 4017 passed, 34 skipped, 0 failed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. PresentsTheCertificate in the Windows build compares the presented certificate's DER with the file's
