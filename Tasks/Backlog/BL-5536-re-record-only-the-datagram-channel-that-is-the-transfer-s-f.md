---
id: BL-5536
title: Re-record only the datagram channel that is the transfer's first connection
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs, Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs, Curl.Engine.UnitTests/EndPointRecordingDatagramConnectorTests.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5536 — Re-record only the datagram channel that is the transfer's first connection

## Goal

`ConnectionEndPointRecorder.ReRecordDatagramChannel` changes the recorded end points only when the
channel re-recording is the transfer's first datagram channel and that channel is also the transfer's
first recorded connection, as its doc comment already claims.

## Context

Found by the shift-end review of 2026-11-18 (ADR-0710), on the diff
d59880ac67...dac3040faf (BL-5532). The code-reviewer and type-design-analyzer reviewers found it
independently.

- `Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.EndPoints.cs:51-60`: `ReRecordDatagramChannel`
  guards only on `DatagramOpenedAt is null || remote is null`. It then overwrites `remoteEndPoint`
  (the first connection's peer) and `DatagramLocalEndPoint` unconditionally.
- `Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:35-66`: every channel the connector
  opens is wrapped in a `ReRecordingChannel`, each with its own `_recorded` flag. So each channel
  calls `ReRecordDatagramChannel` on its first receive.
- Two consequences, both against the recorder's first-wins rule (`Record`, `RecordDatagramChannel`):
  1. **TCP or Unix socket first, then UDP in one transfer.** The UDP server replaces the TCP peer in
     `remoteEndPoint`. `%{remote_ip}` and `%{remote_port}` then name the UDP server, while
     `localEndPoint`, `unixSocketRemoteIp` and `FirstConnectTimings` still describe the first
     connection.
  2. **A second datagram channel in one transfer.** Its end points replace the first channel's,
     while `DatagramOpenedAt` keeps the first channel's time.
- No handler reaches this today; checked on 2026-11-19. TFTP, CoAP, DnsQuery (UDP, then TCP only
  after the UDP answer) and WebTransport each open one datagram channel before any TCP connection.
  The invariant is unenforced, so the next handler that opens TCP first or two channels reports the
  wrong `%{remote_ip}` silently.
- Possible fix: `RecordDatagramChannel` marks the channel it recorded, or reports whether it did and
  whether `Record` made that channel the first connection. `EndPointRecordingDatagramConnector` then
  wraps only that channel; every other channel is returned unwrapped.

## Acceptance criteria

- [ ] A unit test opens a TCP connection through the recorder (`Record` with an IP remote), then a
      datagram channel through `EndPointRecordingDatagramConnector`, and receives on it. The
      report's `RemoteEndPoint` is still the TCP peer.
- [ ] A unit test opens two datagram channels through `EndPointRecordingDatagramConnector`, and the
      second one receives after it moved address. `recorder.DatagramLocalEndPoint` and the report's
      `RemoteEndPoint` still describe the first channel.
- [ ] `EndPointRecordingDatagramConnectorTests.ReceiveAsync_ChannelMovedToTheNextAddress_ReportsTheEndPointsOfTheSecondAddress`
      still passes.
- [ ] The doc comments of `ReRecordDatagramChannel` and `ReRecordingChannel` state the condition the
      code enforces.
- [ ] `dotnet build` is clean, the fast tests are green, and `Curl.Engine.UnitLibrary` keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-19: Created.
