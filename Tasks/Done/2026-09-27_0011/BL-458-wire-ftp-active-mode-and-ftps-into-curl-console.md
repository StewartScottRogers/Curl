---
id: BL-458
title: Wire FTP active mode and ftps:// into Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-437, BL-456, BL-457]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-458 — Wire FTP active mode and ftps:// into Curl.Console

## Goal

`curl -P - ftp://host/f`, `curl --ssl-reqd ftp://host/f` and `curl ftps://host/f` work from the command line: the options reach the transfer context, `FtpProtocolHandler` is built with the listener and TLS provider, and `ftps` is routed and listed as curl 8.21.0 lists it.

## Context

- ADR-0102, "Contract additions", item 4. The handler work is BL-437, the listener BL-456, the options BL-457.
- `Curl.Console/TransferContextFactory.cs` maps `CommandLineOptions` to `TransferContext` (see `FtpDisableEpsv`); `Curl.Console/CurlComposition.cs` builds `new FtpProtocolHandler(connector)` and wraps it in `RoutingFtpProtocolHandler`, which serves only `ftp` today.
- `ftps` through an HTTP proxy: check how curl 8.21.0 treats it before routing it (ADR-0056, rule 3) and record the answer under Notes.
- `-V` lists protocols per ADR-0021; add `ftps` only as the platform's curl lists it.
- BL-466 (ADR-0108) added `FtpProtocolHandler(IConnector, IConnectionListener, ITlsProvider, IDnsResolver)`: build the handler with that constructor and the composition's `IDnsResolver` (the one `TcpConnector` gets), so a `-P` host name resolves.
- BL-474 (ADR-0110) added a five-argument constructor that also takes an `INetworkInterfaceLookup`: pass `new SystemNetworkInterfaceLookup()` (Curl.Networking) so `-P lo` announces the interface address off Windows; it finds nothing on Windows.

## Acceptance criteria

- [x] `TransferContextFactory` maps `FtpPort`, `FtpUseEprt`, `SslLevel` and `FtpSslControlOnly`; each pinned by a named test in `Curl.Console.UnitTests`.
- [x] `CurlComposition` registers a handler for `ftps` and builds `FtpProtocolHandler` with `TcpConnectionListener` and the TLS provider; pinned by a named test.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for `Curl.Console`.

## Notes

Filed by BL-437 under ADR-0102.

- **ftps through an HTTP proxy (measured, curl 8.21.0 Schannel, Record-CurlExchange.ps1):** `curl -sS -x http://127.0.0.1:18458 ftps://h/f` sends `CONNECT h:990 HTTP/1.1` (Host `h:990`, `User-Agent: curl/8.21.0`, `Proxy-Connection: Keep-Alive`) even without `-p`, then fails the handshake against the canned 200 with exit 35. `ftp://h/f` through the same proxy is forwarded as `GET ftp://h/f HTTP/1.1`. So `RoutingFtpProtocolHandler` now serves the FTP handler's schemes (`ftp`, `ftps`) and forwards only `ftp`; `ftps` always goes to `FtpProtocolHandler`.
- **`-V`:** the platform's curl lists `dict file ftp ftps gopher gophers ...`. `CurlVersionText.ProtocolsLine` lacked `ftp` too (missed by BL-434), and ADR-0021 Decision 6 has the task that registers a handler update it in the same change, so this task added `ftp ftps`. That lives in `Curl.Cli.UnitLibrary`/`Curl.Cli.UnitTests`, which no task in Doing touches (BL-480: Http; BL-481: Networking), so both were added to `touches`.
- **Composition:** `CreateProtocolHandlers` now takes the `ITlsProvider` and `IDnsResolver`; the production path passes `CurlTransports.TlsProvider` and `DnsResolver` (the ones `TcpConnector` has), and `CreateFtpProtocolHandler` adds a `TcpConnectionListener` and `SystemNetworkInterfaceLookup`. The fake-connector `CreateRunner` overload (used by Conformance tests, whose signature stays) builds an `SslStreamTlsProvider` from the command line's TLS options and a `SystemDnsResolver`; neither opens a socket unless a transfer uses `-P` with a host name or TLS.
- Tests: `TransferContextFactoryTests` (5 new: defaults, `-P`, `--disable-eprt`, `--ssl`/`--ssl-reqd`, `--ftp-ssl-control`), `CurlCompositionTests.CreateTransferDispatch_ProductionTransports_FtpHandlerGetsTheListenerTlsProviderAndResolver` and `CreateRunner_FtpsUrlWithFakeConnectors_ReachesConnectorAtPort990WithTls`, `RoutingFtpProtocolHandlerTests` (ftps through HTTP proxy; schemes). Curl.Console and Curl.Cli.UnitLibrary: 100% line and branch, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl -P, --disable-eprt, --ssl/--ssl-reqd, --ftp-ssl-control and ftps:// reach FtpProtocolHandler, built with the listener, TLS provider and resolver; -V lists ftp ftps
