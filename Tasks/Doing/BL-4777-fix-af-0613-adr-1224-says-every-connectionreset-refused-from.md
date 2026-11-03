---
id: BL-4777
title: Fix AF-0613: ADR-1224 says every ConnectionReset/Refused from a send or receive becomes exit 7; the channel converts it only until its first datagram arrives
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-11-03
completed:
---
# BL-4777 — Fix AF-0613: ADR-1224 says every ConnectionReset/Refused from a send or receive becomes exit 7; the channel converts it only until its first datagram arrives

## Goal

The defect the audit office reported as AF-0613 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0613 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0613-adr-1224-says-every-connectionreset-refused-from-a.md`.

Location: `Documentation/Planning/Decisions/ADR-1224-closed-udp-port-is-exit-7.md:15`

Location: `Documentation/Planning/Decisions/ADR-1224-closed-udp-port-is-exit-7.md:15`

ADR-1224 Decision, line 15: "PortUnreachableReportingDatagramChannel, which turns a ConnectionReset or ConnectionRefused from a send or receive into a DatagramPortUnreachableException carrying the connector's own exit 7 text". Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs:12 says it does so "only until the channel has received its first datagram (BL-4675): a port that refuses after the server has answered is not 'failed to connect', so that SocketException passes through unchanged for the handler". UdpDatagramConnector.cs:201 says the same. The ADR's rule is broader than the code's.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1224-*.md -Pattern 'from a send or receive'; Select-String -Path Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs -Pattern 'only until the channel has received its first datagram'
```

- Expected: The ADR states the first-datagram limit, or no limit exists in the code.
- Actual: ADR-1224:15 states no limit; PortUnreachableReportingDatagramChannel.cs:12 limits the conversion to before the first datagram (BL-4675).

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

ADR-1224 line 15 now states the first-datagram limit (BL-4675). Docs only; no code touched.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
