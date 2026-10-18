---
id: AF-0240
title: IDatagramConnector's doc calls TFTP the one datagram protocol using it; QUIC, DNS and the Kerberos KDC transport open through it too
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-10-18_1045.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Protocol.Abstractions.UnitLibrary/IDatagramConnector.cs:IDatagramConnector:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed: 2026-10-18
closed-how: reliable-reaudit
closed-by: 2026-10-18_1045.md
---
# AF-0240 - IDatagramConnector's doc calls TFTP the one datagram protocol using it; QUIC, DNS and the Kerberos KDC transport open through it too

## Summary

Low finding from the truthfulness auditor at `Curl.Protocol.Abstractions.UnitLibrary/IDatagramConnector.cs:4`: IDatagramConnector's doc calls TFTP the one datagram protocol using it; QUIC, DNS and the Kerberos KDC transport open through it too.

## Evidence

Location: `Curl.Protocol.Abstractions.UnitLibrary/IDatagramConnector.cs:4`

Summary, lines 4-5: 'Opens an IDatagramChannel to a host and port: the seam TFTP, the one datagram protocol, acquires its transport through, once per transfer.' It is also used by Curl.Networking.UnitLibrary/DatagramConnectorUdpChannelOpener.cs:19 (QUIC channels, 'the transport the run's datagram protocols use'), DatagramConnectorDnsSocketOpener.cs:16 (DNS UDP queries), KerberosKdcSocketTransport.cs:19 (KDC over UDP) and Curl.Conformance.Http3Server.UnitLibrary/Http3ServerConnector.cs (HTTP/3 stand-in). Seed 0x16500922; type 29 of the 30 drawn.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Curl.Protocol.Abstractions.UnitLibrary/IDatagramConnector.cs -Pattern 'the seam TFTP, the one').Count; (Select-String -Path Curl.Networking.UnitLibrary/DatagramConnectorUdpChannelOpener.cs,Curl.Networking.UnitLibrary/DatagramConnectorDnsSocketOpener.cs,Curl.Networking.UnitLibrary/KerberosKdcSocketTransport.cs -Pattern '\(IDatagramConnector datagramConnector').Count
```

- Expected: 0 then 3: the doc names every datagram user
- Actual: 1 then 3: the doc says TFTP is the only one while three other transports use it

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: no | Ran the reproduction: 'the seam TFTP, the one' matches 0 lines in IDatagramConnector.cs (its summary now reads 'the seam every datagram transport acquires its channel through. TFTP opens one per transfer; QUIC channels, DNS ...'); the three Networking openers still take an IDatagramConnector (5 matches).

## Log

- 2026-10-16: filed proposed.
- 2026-10-18: proposed -> closed. Re-audit 2026-10-18_1045.md: the reproduction no longer reproduces.
