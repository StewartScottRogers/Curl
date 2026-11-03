---
id: BL-4636
title: Fix AF-0530: Surviving mutant: TcpConnector.Opened can report every fresh connection as reused without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4636 — Fix AF-0530: Surviving mutant: TcpConnector.Opened can report every fresh connection as reused without a test failing

## Goal

The defect the audit office reported as AF-0530 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0530 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0530-surviving-mutant-tcpconnector-opened-can-report-ev.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2369`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2369`

Mutant 'isReused: false,' -> 'isReused: true,' in the ConnectResult.Connected call of Opened survived (Curl.Networking.UnitTests all green, 3 baseline failures excluded). ConnectResult.IsReused drives decisions across the protocols. HttpProtocolHandler passes 'newConnection: !connect.IsReused', which governs the resend of a request that fails on a reused connection, so request bytes can be sent twice. FtpProtocolHandler, ImapProtocolHandler, Pop3ProtocolHandler and SshProtocolHandler resume a kept session when IsReused, CertificateCheckHooks skips checks for a reused connection, and EndPointRecordingConnector records reuse for write-out. A fresh connection marked reused changes request bytes and exit codes.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:2369:false -Member Opened -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
