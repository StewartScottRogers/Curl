---
id: BL-2010
title: Count the CONNECT reply head in %{size_header} through -p (upstream test1288, GF-0045)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2010 — Count the CONNECT reply head in %{size_header} through -p (upstream test1288, GF-0045)

## Goal

Through a `-p` CONNECT tunnel, `%{size_header}` counts the proxy's CONNECT reply head as curl 8.21.0 does, `--suppress-connect-headers` or not, so upstream test1288 passes.

## Context

- Split from BL-1975 (GF-0045), whose lane could not touch `Curl.Protocol.Http.UnitLibrary` (held by BL-1959).
- Measured in `Curl.Conformance.UnitTests` (in-process over `TcpConnector`): test1288 fails only at `RECEIVED HEADER BYTE TOTAL: 231` expected, `170` got. The 61 missing bytes are the CONNECT reply head (`HTTP/1.1 200 Mighty fine indeed\r\n`, `Server: test tunnel 2000\r\n`, `\r\n`); everything else - suppressed `-D -`/`-i` lines, `%{http_connect}` 200 - already matches.
- Where: `ConnectResult` (Abstractions) carries no CONNECT head size; `TcpConnector` knows it (`HttpProxyTunnelReply.Head`, every reply of the tunnel, 407s included as curl counts all); `HttpProtocolHandler` builds `TransferReport.HeaderSize` (around line 2613/2641). Add e.g. `ConnectResult.ProxyConnectHeaderBytes`, pass it through `PoolingConnector` (a reused connection reports 0) and add it to `HeaderSize`.

## Acceptance criteria

- [x] `UpstreamCase_RunThroughCurl_HoldsTheRatchet(1288)` passes and 1288 is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] Unit tests in `Curl.Networking.UnitTests` and `Curl.Protocol.Http.UnitTests` pin the byte count (one CONNECT, and a 407 then 200).
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- 2026-10-10: `ConnectResult.ProxyConnectHeaderBytes` (Abstractions) carries the bytes of every CONNECT reply head; `TcpConnector` counts them in a `StrongBox<long>` on `TunnelRequest`, created once outside the redial loop so a 407 answered on a closed-and-redialled connection counts too, and adds the total in `WithConnectReplyHeaders`. `PoolingConnector` passes it on for a new connection only (a reused one reports 0). `HttpProtocolHandler` seeds `TransferReport.HeaderSize` with it when there is no earlier report (a retry's earlier report already holds it). CONNECT-UDP and h2 proxy tunnels are not counted (not measured; no upstream case asks).
- Tests: `TcpConnectorTests` (test1288's 61-byte reply; a 407 then 200), `PoolingConnectorTests` (opened 61, reused 0), `ConnectResultTests`, `HttpProtocolHandlerTests.ProxyConnectHeaderSize` (0, 61, 152). Fast tests: 34 projects green; test1288 added to PassingUpstreamCases.txt and passes. New code adds no branch, so Measure-CodeQuality was not run.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. %{size_header} through a -p CONNECT tunnel counts every CONNECT reply head; upstream test1288 passes
