---
id: BL-777
title: Report a WebSocket transfer's written payload bytes as %{size_delivered}
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-516, BL-582]
touches: [Curl.Protocol.Ws.UnitLibrary, Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-777 — Report a WebSocket transfer's written payload bytes as %{size_delivered}

## Goal

`%{size_delivered}` on a `ws`/`wss` transfer prints the payload bytes written to the output, as curl 8.21.0 does, while `%{size_download}` keeps printing the frame bytes received.

## Context

- Found in BL-582, measured 2026-09-28 with curl 8.21.0 (Schannel) and `Record-CurlExchange.ps1`: `-w '%{size_download} %{size_delivered}'` prints `14 8` for `81 03 "one"` `81 03 "two"` `88 02 03 e8`, `9 3` for a fragmented binary message `02 02 01 02` `80 01 03` `88 00`, `7 5` for `81 05 "hello"` then a dropped connection, and `5 3` for a frame cut short at `hel`.
- Today `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` prints `DownloadSize` for both. BL-516 separates the two in `TransferReport`; once it lands, `WsProtocolHandler.ExchangeFramesAsync` sets the new field from the bytes it passes to the output.

## Acceptance criteria

- [ ] `Curl.Protocol.Ws.UnitTests` pin the delivered size for each of the four measured cases above, beside the download size.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ws.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
