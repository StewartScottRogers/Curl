---
id: AF-0555
title: PresentsTheCertificate checks the presented client certificate only for null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
key: quality:Curl.Networking.UnitTests/TcpConnectorQuicTests.TlsOptions.cs:ConnectMultiplexedAsync_InTheWindowsBuildWithCertInACertificateFile_PresentsTheCertificate:weak-assertion
reproduction: none
task: BL-4703
tasks: BL-4703
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1017.md, 2026-11-03_1615.md
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

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | No match for 'ClientCertificate\);'; both tests assert CollectionAssert.AreEqual(s_clientCertificate.RawData, presented.CertificateData).
- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | No 'ClientCertificate);' match; the tests compare the presented certificate (ClientCertificate!.CertificateList.Single()).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
