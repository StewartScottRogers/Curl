---
id: BL-2577
title: Fix AF-0240: IDatagramConnector's doc calls TFTP the one datagram protocol using it; QUIC, DNS and the Kerberos KDC transport open through it too
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary]
requirement: none
created: 2026-10-17
completed:
---
# BL-2577 — Fix AF-0240: IDatagramConnector's doc calls TFTP the one datagram protocol using it; QUIC, DNS and the Kerberos KDC transport open through it too

## Goal

The defect the audit office reported as AF-0240 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0240 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0240-idatagramconnector-s-doc-calls-tftp-the-one-datagr.md`.

Location: `Curl.Protocol.Abstractions.UnitLibrary/IDatagramConnector.cs:4`

Location: `Curl.Protocol.Abstractions.UnitLibrary/IDatagramConnector.cs:4`

Summary, lines 4-5: 'Opens an IDatagramChannel to a host and port: the seam TFTP, the one datagram protocol, acquires its transport through, once per transfer.' It is also used by Curl.Networking.UnitLibrary/DatagramConnectorUdpChannelOpener.cs:19 (QUIC channels, 'the transport the run's datagram protocols use'), DatagramConnectorDnsSocketOpener.cs:16 (DNS UDP queries), KerberosKdcSocketTransport.cs:19 (KDC over UDP) and Curl.Conformance.Http3Server.UnitLibrary/Http3ServerConnector.cs (HTTP/3 stand-in). Seed 0x16500922; type 29 of the 30 drawn.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Protocol.Abstractions.UnitLibrary/IDatagramConnector.cs -Pattern 'the seam TFTP, the one').Count; (Select-String -Path Curl.Networking.UnitLibrary/DatagramConnectorUdpChannelOpener.cs,Curl.Networking.UnitLibrary/DatagramConnectorDnsSocketOpener.cs,Curl.Networking.UnitLibrary/KerberosKdcSocketTransport.cs -Pattern '\(IDatagramConnector datagramConnector').Count
```

- Expected: 0 then 3: the doc names every datagram user
- Actual: 1 then 3: the doc says TFTP is the only one while three other transports use it

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
