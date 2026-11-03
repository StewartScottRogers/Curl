---
id: AF-0555
title: PresentsTheCertificate checks the presented client certificate only for null
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Networking.UnitTests/TcpConnectorQuicTests.TlsOptions.cs:ConnectMultiplexedAsync_InTheWindowsBuildWithCertInACertificateFile_PresentsTheCertificate:weak-assertion
reproduction: none
task: BL-4703
tasks: BL-4703
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0555 - PresentsTheCertificate checks the presented client certificate only for null

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/TcpConnectorQuicTests.TlsOptions.cs:67`: PresentsTheCertificate checks the presented client certificate only for null. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Networking.UnitTests/TcpConnectorQuicTests.TlsOptions.cs:67`

'Assert.IsNotNull(opener.Opened.Single().Server!.Tls!.ClientCertificate);' is the only check of the certificate; the test never compares it with s_clientCertificate from both.pem.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/TcpConnectorQuicTests.TlsOptions.cs -Pattern 'ClientCertificate\);'
```

- Expected: The presented certificate's DER is compared with the file's certificate.
- Actual: TcpConnectorQuicTests.TlsOptions.cs:67: Assert.IsNotNull(opener.Opened.Single().Server!.Tls!.ClientCertificate);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
