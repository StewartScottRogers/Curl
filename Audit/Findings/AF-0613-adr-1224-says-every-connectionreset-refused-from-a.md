---
id: AF-0613
title: ADR-1224 says every ConnectionReset/Refused from a send or receive becomes exit 7; the channel converts it only until its first datagram arrives
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:Documentation/Planning/Decisions/ADR-1224-closed-udp-port-is-exit-7.md:PortUnreachableReportingDatagramChannel:stale-adr
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0613 - ADR-1224 says every ConnectionReset/Refused from a send or receive becomes exit 7; the channel converts it only until its first datagram arrives

## Summary

Low finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1224-closed-udp-port-is-exit-7.md:15`: ADR-1224 says every ConnectionReset/Refused from a send or receive becomes exit 7; the channel converts it only until its first datagram arrives.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1224-closed-udp-port-is-exit-7.md:15`

ADR-1224 Decision, line 15: "PortUnreachableReportingDatagramChannel, which turns a ConnectionReset or ConnectionRefused from a send or receive into a DatagramPortUnreachableException carrying the connector's own exit 7 text". Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs:12 says it does so "only until the channel has received its first datagram (BL-4675): a port that refuses after the server has answered is not 'failed to connect', so that SocketException passes through unchanged for the handler". UdpDatagramConnector.cs:201 says the same. The ADR's rule is broader than the code's.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Documentation/Planning/Decisions/ADR-1224-*.md -Pattern 'from a send or receive'; Select-String -Path Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs -Pattern 'only until the channel has received its first datagram'
```

- Expected: The ADR states the first-datagram limit, or no limit exists in the code.
- Actual: ADR-1224:15 states no limit; PortUnreachableReportingDatagramChannel.cs:12 limits the conversion to before the first datagram (BL-4675).

## Re-audits

## Log

- 2026-11-03: filed proposed.
