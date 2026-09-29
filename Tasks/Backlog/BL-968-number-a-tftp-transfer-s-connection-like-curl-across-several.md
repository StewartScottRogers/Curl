---
id: BL-968
title: Number a TFTP transfer's connection like curl across several URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-933]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-968 — Number a TFTP transfer's connection like curl across several URLs

## Goal

`curl -v tftp://h/a tftp://h/b` (and a TFTP URL after an HTTP one) prints `shutting down connection #N` with the number curl 8.21.0 gives that connection, where today TFTP always prints `#0`.

## Context

- BL-933 made `TftpTransferEvents.ShuttingDown` report `shutting down connection #0`: `IDatagramConnector`/`DatagramOpenResult` carry no connection number, unlike `ConnectResult.ConnectionNumber`, so the handler cannot know it. Right for a single transfer, which is all BL-933 measured.
- Measure first with `Record-CurlExchange.ps1 -Tftp` (it serves one transfer, so two URLs may need the recorder extended): two TFTP URLs in one invocation, and an HTTP URL followed by a TFTP one, with `-v`.
- Likely shape: a connection number on `DatagramOpenResult` assigned from the same counter `TcpConnector`/`PoolingConnector` use, read by `TftpProtocolHandler`.

## Acceptance criteria

- [ ] Measured `-v` output for the two cases is copied into Notes with curl's version.
- [ ] A `Curl.Protocol.Tftp.UnitTests` test pins `shutting down connection #N` with the number the channel was opened with.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

## Log

- 2026-09-29: Created.
