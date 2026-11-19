---
id: BL-5532
title: Re-record the UDP channel's end points after PortUnreachableReportingDatagramChannel moves to the next address
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs, Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5532 — Re-record the UDP channel's end points after PortUnreachableReportingDatagramChannel moves to the next address

## Goal

After a UDP transfer moves from a refused address to the next resolved one (EXT-100), %{remote_ip}, %{remote_port}, %{local_ip} and %{local_port} report the socket that carried the transfer, not the refused one.

## Context

Found by the shift-end review of 2026-11-18 (type-design-analyzer, ADR-0710), diff 2a473a41ba..43a2df5f77.
`Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:31-32` records ServerEndPoint and LocalEndPoint once, right after OpenAsync, and `ConnectionEndPointRecorder.RecordDatagramChannel` (`ConnectionEndPointRecorder.EndPoints.cs:35`) keeps only the first record. `PortUnreachableReportingDatagramChannel.SwitchToNextAddressAsync` (`Curl.Networking.UnitLibrary/PortUnreachableReportingDatagramChannel.cs:87`) later swaps `_inner` to a channel opened to the next address, and nothing records again. So for `localhost` with only an IPv4 listener, `::1` refuses, the transfer runs over 127.0.0.1, but the write-out still shows the disposed `::1` socket - the case EXT-100 was meant to fix.

## Acceptance criteria

- [x] The end points are read from the channel once it carries the transfer (for example when the first datagram is received, or through a notification when the channel switches), not only at open.
- [x] A unit test with a stand-in datagram connector whose first address refuses shows %{remote_ip} and %{local_port} of the second address.
- [x] A transfer with no switch reports the same end points as before.
- [x] `dotnet build` is clean, the fast tests pass, and Curl.Engine.UnitLibrary and Curl.Networking.UnitLibrary keep 100% line and branch coverage.

## Notes

Chosen: `EndPointRecordingDatagramConnector` wraps each opened channel so its first received datagram re-records the channel's server and local end points (`ConnectionEndPointRecorder.ReRecordDatagramChannel`); no change was needed in Curl.Networking (the channel already exposes the swapped inner end points). The open result keeps its connection number. Tests: `EndPointRecordingDatagramConnectorTests`. Quality run for Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 17639dd5 d97db41e eee9499a e06af265.
