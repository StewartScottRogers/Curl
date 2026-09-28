---
id: BL-397
title: Give the TFTP handler the proxy connector in Curl.Console so tftp:// sends its MASQUE request
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-345]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-397 — Give the TFTP handler the proxy connector in Curl.Console so tftp:// sends its MASQUE request

## Goal

`curl -x http://<proxy> tftp://…` run through `Curl.Console` writes the measured MASQUE `connect-udp` request to the proxy and ends with exit 7 `curl: (7) bind() failed; Invalid arguments`.

## Context

- ADR-0056, rule 4. BL-345 made `TftpProtocolHandler` take an optional `IConnector` (the proxy connector) and an optional credential `Encoding`; without the connector it still fails with exit 7 but sends nothing to the proxy.
- `Curl.Console/CurlComposition.cs` builds `new TftpProtocolHandler(datagramConnector)` (line ~55). Pass the TCP connector the other handlers get (not wrapped in a proxy tunnel: the handler connects to the proxy itself) and `CredentialEncoding.ForPlatform(...)`, as ADR-0059 does for the CONNECT tunnel.
- Measured request bytes are pinned in `Curl.Protocol.Tftp.UnitTests/TftpHttpProxyTests.cs`.

## Acceptance criteria

- [x] `CurlComposition` passes a TCP `IConnector` and the platform credential encoding to `TftpProtocolHandler`.
- [x] A `Curl.Console.UnitTests` test runs `-x http://127.0.0.1:18331 tftp://example.com/f` against a scripted connector and asserts the connector received the BL-330 request bytes, stderr is `curl: (7) bind() failed; Invalid arguments`, and the exit code is 7.
- [x] `dotnet build` clean and fast tests green.

## Notes

- Delivered: `CurlComposition.CreateProtocolHandlers` passes its TCP connector (the pooling connector in production) and `CredentialEncoding.ForPlatform` to `TftpProtocolHandler`. The handler marks its target `IsForwardProxy`, so the connector dials the proxy directly, as it does for forwarded HTTP. Tests: `CurlCompositionProxyTests.RunAsync_TftpUrlThroughAnHttpProxy_*`; the pooling-connector pin in `CurlCompositionTests` now lists `TftpProtocolHandler`. The request bytes are the BL-330 measurement, so no new measurement was needed.
- The SOCKS and HTTPS proxy kinds this note once left unmeasured were measured and handled by BL-398 (ADR-0096); the HTTPS proxy now also reaches the handler through this wiring.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. tftp:// through an HTTP proxy in Curl.Console sends the MASQUE request to the proxy and exits 7
