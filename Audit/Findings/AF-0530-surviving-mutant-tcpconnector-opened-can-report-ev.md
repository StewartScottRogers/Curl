---
id: AF-0530
title: Surviving mutant: TcpConnector.Opened can report every fresh connection as reused without a test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_0025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TcpConnector.cs:Opened-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.cs:2369:false
task: BL-4636
tasks: BL-4636
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_0025.md
---
# AF-0530 - Surviving mutant: TcpConnector.Opened can report every fresh connection as reused without a test failing

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.cs:2369`: Surviving mutant: TcpConnector.Opened can report every fresh connection as reused without a test failing. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2369`

Mutant 'isReused: false,' -> 'isReused: true,' in the ConnectResult.Connected call of Opened survived (Curl.Networking.UnitTests all green, 3 baseline failures excluded). ConnectResult.IsReused drives decisions across the protocols. HttpProtocolHandler passes 'newConnection: !connect.IsReused', which governs the resend of a request that fails on a reused connection, so request bytes can be sent twice. FtpProtocolHandler, ImapProtocolHandler, Pop3ProtocolHandler and SshProtocolHandler resume a kept session when IsReused, CertificateCheckHooks skips checks for a reused connection, and EndPointRecordingConnector records reuse for write-out. A fresh connection marked reused changes request bytes and exit codes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:2369:false -Member Opened -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: no | Ran the -Site command: outcome killed at resolvedLine 2369 ('isReused: false,' -> 'isReused: true,'); the baseline left out three failing HandBuiltTlsProviderTests ECH tests. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_0025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
