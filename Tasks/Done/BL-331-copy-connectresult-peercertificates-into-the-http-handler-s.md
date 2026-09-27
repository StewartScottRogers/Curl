---
id: BL-331
title: Copy ConnectResult.PeerCertificates into the HTTP handler's TransferReport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-303]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-331 — Copy ConnectResult.PeerCertificates into the HTTP handler's TransferReport

## Goal

`curl -k -w "%{num_certs}
%{certs}" https://...` prints the server's certificate chain instead of `0` and nothing, because the HTTP handler copies the chain from its `ConnectResult` into `TransferReport.PeerCertificates`.

## Context

- ADR-0054 (BL-303): `SslStreamTlsProvider` captures the chain as DER on `ConnectResult.PeerCertificates`, `TcpConnector` passes it on, and `%{num_certs}`/`%{certs}` print `TransferReport.PeerCertificates`. Each TLS handler must copy it into its report; BL-303 could not touch `Curl.Protocol.Http.UnitLibrary`.
- Copy it where `HttpProtocolHandler` already copies `LocalEndPoint = connect.LocalEndPoint` (`HttpProtocolHandler.cs`, around line 626). On a redirect or reconnect, the report carries the last connection's chain, as curl reports the last transfer's certinfo.

## Acceptance criteria

- [x] A test in `Curl.Protocol.Http.UnitTests` gives the handler a `ConnectResult` with two certificates and asserts `TransferReport.PeerCertificates` holds the same two, in order.
- [x] A plain http:// transfer's report has an empty `PeerCertificates`, pinned in a test.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Pipeline run compressed: the change is one property copy in `HttpExchange.Report` (`PeerCertificates = connect.PeerCertificates`), so no architect plan or ADR was needed; ADR-0054 already decides the behaviour. Each exchange builds its report from its own `ConnectResult`, so a redirect or reconnect reports the last connection's chain.
- Tests: `ExecuteAsync_ConnectWithPeerCertificates_ReportsTheSameChainInOrder`, `ExecuteAsync_PlainHttp_ReportsNoPeerCertificates` in `HttpProtocolHandlerTests`. Http.UnitTests 744 passed; Measure-CodeQuality: 100% line, 100% branch, worst CRAP 10.
- `dotnet format --verify-no-changes` reports end-of-line errors in `Curl.Protocol.Telnet.UnitTests/TelnetProtocolHandlerTests.cs`; pre-existing and outside this task's touches, left alone.

## Log

- 2026-09-27: Created.
- 2026-09-27: Filed by BL-303 (replaces the BL-314 reference lost when another lane took that ID).
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. HTTP(S) transfer reports carry the connection's peer certificate chain, so %{num_certs} and %{certs} print it
