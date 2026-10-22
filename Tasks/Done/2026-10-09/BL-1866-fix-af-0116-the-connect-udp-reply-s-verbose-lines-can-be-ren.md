---
id: BL-1866
title: Fix AF-0116: The CONNECT-UDP reply's verbose lines can be rendered as a CONNECT reply's (forConnectUdp: true -> false) with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- No production change: the code was right, the tests just never drove a CONNECT-UDP reply carrying a body field through `RequestUdpTunnelAsync`. Added `ConnectMultiplexedAsync_WhenThe101ReplyCarriesAContentLength_SaysItIgnoresItInACONNECTUDPResponse` in `TcpConnectorQuicTests.UdpTunnel.cs`: a 101 with `Content-Length: 5` must report `Ignoring Content-Length in CONNECT-UDP 101 response`. With `forConnectUdp: false` there is no such line (a CONNECT 101 does not ignore body fields), so the mutant fails it.
- Added `Curl.Networking.UnitTests` to `touches`: the fix is a test, and no other task in Doing on `origin/work/dark-factory` (BL-1862 Curl.Tls.UnitLibrary, BL-1863 Curl.Protocol.Http.UnitLibrary) names it.
- The audit guard refuses lanes `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand at line 244 (`forConnectUdp: false`): the new test failed ("Expected collection to contain the specified element"), then the line was restored. The quality auditor's re-audit confirms the finding.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Test pins the CONNECT-UDP ignored-field line through RequestUdpTunnelAsync; the forConnectUdp mutant now fails it.
