---
id: BL-1866
title: Fix AF-0116: The CONNECT-UDP reply's verbose lines can be rendered as a CONNECT reply's (forConnectUdp: true -> false) with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-09
completed:
---
# BL-1866 — Fix AF-0116: The CONNECT-UDP reply's verbose lines can be rendered as a CONNECT reply's (forConnectUdp: true -> false) with no test failing

## Goal

The defect the audit office reported as AF-0116 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0116 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0116-the-connect-udp-reply-s-verbose-lines-can-be-rende.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:244`

Location: `Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:244`

Mutant 'ConnectTunnelVerboseLines.ReportReplyHead(..., forConnectUdp: true)' -> 'forConnectUdp: false' survived. In ConnectTunnelVerboseLines.ReportReplyHead (line 156-160) the flag picks HttpProxyTunnel.IgnoresBodyFields(statusCode, forConnectUdp) and the label of the ignored-field line, ' in CONNECT-UDP <code> response' vs ' in CONNECT <code> response' (measured, BL-1399). So curl -v through an HTTP/3 UDP tunnel would write a different line to stderr. The UDP-tunnel tests do not pin the reply-head lines for a reply that carries body fields.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs:244:true -Member RequestUdpTunnelAsync -ExcludeBaselineFailures -TimeoutSeconds 600
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
- 2026-10-09: Backlog -> Doing.
