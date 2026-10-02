---
id: BL-969
title: Number a TFTP transfer's connection like curl across several URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-933]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-02
---
# BL-969 — Number a TFTP transfer's connection like curl across several URLs

## Goal

`curl -v tftp://h/a tftp://h/b` (and a TFTP URL after an HTTP one) prints `shutting down connection #N` with the number curl 8.21.0 gives that connection, where today TFTP always prints `#0`.

## Context

- BL-933 made `TftpTransferEvents.ShuttingDown` report `shutting down connection #0`: `IDatagramConnector`/`DatagramOpenResult` carry no connection number, unlike `ConnectResult.ConnectionNumber`, so the handler cannot know it. Right for a single transfer, which is all BL-933 measured.
- Measure first with `Record-CurlExchange.ps1 -Tftp` (it serves one transfer, so two URLs may need the recorder extended): two TFTP URLs in one invocation, and an HTTP URL followed by a TFTP one, with `-v`.
- Likely shape: a connection number on `DatagramOpenResult` assigned from the same counter `TcpConnector`/`PoolingConnector` use, read by `TftpProtocolHandler`.

## Acceptance criteria

- [x] Measured `-v` output for the two cases is copied into Notes with curl's version.
- [x] A `Curl.Protocol.Tftp.UnitTests` test pins `shutting down connection #N` with the number the channel was opened with.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library touched.

## Notes

- Measured 2026-10-02 on curl 8.21.0 (x86_64-w64-mingw32, Schannel; Git's build) with
  Record-CurlExchange.ps1 -Tftp. The recorder serves one transfer, so a second recorder on
  another port served the second URL (its own curl replaced by ping); the recorder was not
  extended. -sv lines:
  - tftp://127.0.0.1:46995/a tftp://127.0.0.1:46996/b:
    *   Trying 127.0.0.1:46995... ... * shutting down connection #0, then
    *   Trying 127.0.0.1:46996... ... * shutting down connection #1.
  - http://127.0.0.1:46997/a tftp://127.0.0.1:46998/b:
    * Connection #0 to host 127.0.0.1:46997 left intact, then
    *   Trying 127.0.0.1:46998... ... * shutting down connection #1.
  - curl 8.18.0 (LibreSSL, WinGet) printed the same numbers.
- Decision (ADR-0345): DatagramOpenResult.ConnectionNumber + WithConnectionNumber;
  PoolingConnector.NumberingDatagrams wraps a datagram connector so each open (failed ones
  too, as ADR-0109 numbers failed TCP connects) takes the next number from the pool's
  ConnectionCache; CurlComposition.NumberedDatagramsOf gives only the TFTP handler that
  wrapper, so Kerberos KDC datagrams (which curl sends via GSS-API, outside its cache) take no number.
- Touches widened to Curl.Console and Curl.Console.UnitTests: the wiring lives in
  CurlComposition.CreateProtocolHandlers; no task in Doing on origin/work/dark-factory
  named either (only BL-1073, which touches Curl.Protocol.Http).
- Tests: DatagramOpenResultTests (3 new), PoolingConnectorSharedCacheTests (3 new),
  TftpTransferEventsTests.ExecuteAsync_OnAChannelOpenedAsConnection2_ShutsDownConnection2,
  CurlCommandRunnerTftpConnectionNumberTests (both measured cases end to end).
- Quality: Measure-CodeQuality reports 100% line and branch, 0 failing members, for
  Curl.Protocol.Abstractions.UnitLibrary, Curl.Networking.UnitLibrary,
  Curl.Protocol.Tftp.UnitLibrary and Curl.Console.
- One full fast-suite run had a single Curl.Authentication.UnitTests failure (project not
  touched); three reruns of that project and a second full run were green, so it was a flake
  under the parallel lanes' load.
## Log

- 2026-09-29: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. curl -v numbers a TFTP transfer's connection with the run's others: two TFTP URLs shut down #0 then #1, a TFTP URL after an HTTP one #1
