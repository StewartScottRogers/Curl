---
id: BL-5533
title: Send a datagram addressed to the refused address to the new address after PortUnreachableReportingDatagramChannel switches
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5533 — Send a datagram addressed to the refused address to the new address after PortUnreachableReportingDatagramChannel switches

## Goal

After PortUnreachableReportingDatagramChannel moves to the next resolved address, a send addressed to the refused address goes to the new one, so a TFTP retransmit reaches the server it moved to.

## Context

Found by the shift-end review of 2026-11-18 (code-reviewer, ADR-0710), diff 2a473a41ba..43a2df5f77.
`SwitchToNextAddressAsync` (`Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs:87-121`) resends the saved datagrams to `opened.ServerEndPoint`, but `SendAsync` (line 38) forwards later sends to whatever `destination` the caller passes. TFTP captures its destination when it is built (`Curl.Original.Protocol.Tftp.UnitLibrary/TftpDownload.cs:83`, `TftpUpload.cs:82`: `lastDestination = channel.ServerEndPoint`) and `ResendAsync` (`TftpDownload.cs:505`) still sends there. Example: `curl tftp://localhost/f` on Windows, `::1` refuses, the channel switches to an IPv4 socket for 127.0.0.1, and the first retransmit goes to `[::1]:69` through the IPv4 socket. That throws an address-family SocketException that is not a refusal, so the transfer fails where curl would retransmit to the new address. When both addresses share a family, the retransmit silently goes to the refused address.

## Acceptance criteria

- [ ] After a switch, `SendAsync` sends a datagram addressed to any refused channel's ServerEndPoint to the current channel's ServerEndPoint. Other destinations are unchanged.
- [ ] A unit test switches from a refusing first address and then sends to the original ServerEndPoint, and shows the datagram reaches the second address.
- [ ] `dotnet build` is clean, the fast tests pass, and Curl.Networking.UnitLibrary keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-18: Created.
