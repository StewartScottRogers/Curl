---
id: BL-2472
title: Engine: CURLOPT_CONNECT_ONLY perform through a proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2423]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Documentation/Planning/Decisions/ADR-0608-connect-only-recv-and-send.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2472 — Engine: CURLOPT_CONNECT_ONLY perform through a proxy

## Goal

A `CURLOPT_CONNECT_ONLY` 1 perform of the managed engine with a proxy set (HTTP `CONNECT` tunnel, SOCKS4/5, HTTPS proxy) connects through it, as libcurl does, before `curl_easy_recv` and `curl_easy_send` use the connection.

## Context

ADR-0608 (BL-2423): `ManagedEasyTransfer.PerformConnectOnlyAsync` dials `ManagedCurlEngine.CreateConnectOnlyConnector`'s `TcpConnector` with a bare `ConnectTarget` (host, port, TLS) and sets no `ConnectTarget.Proxy`, so a proxy option is ignored. Choose the proxy as a normal perform does (`TransferProxySelection.TrySelect` over `EasyProxyOptions`, honouring the no-proxy list) and put it on the target; libcurl tunnels through an HTTP proxy for connect-only even without `CURLOPT_HTTPPROXYTUNNEL`.

## Acceptance criteria

- [x] A `ManagedEasyTransferTests` test shows a connect-only perform with `EasyProxyOptions.Proxy` set dials a `ConnectTarget` whose `Proxy` names that proxy, and one with the host in the no-proxy list dials none.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [x] ADR-0608 and `Curl.Engine.UnitLibrary/CLAUDE.md` say the proxy is used.

## Notes

- `PerformConnectOnlyAsync` now takes the transfer settings and calls `TransferProxySelection.TrySelect` (proxy, pre-proxy, proxy type, no-proxy list, `-U` from `ToolSettings`, Unix socket) before dialling, putting the proxy on `ConnectTarget.Proxy`; the `TcpConnector` already tunnels (CONNECT for HTTP/HTTPS, handshake for SOCKS) for a target with a proxy. Unusable proxy text ends the perform with the selector's code and text, nothing dialled.
- Decision (recorded as ADR-0608 rule 6): the selector reads no proxy environment variable, because the engine's normal library perform chooses no proxy at all today (`OwnInputsOf`); reading the environment for both is BL-2518, filed for it.
- Added the ADR-0608 file to `touches` (the criterion names it); no task in Doing names it.
- Tests: `ManagedEasyTransferTests.ConnectOnly.cs` - HTTP proxy, SOCKS5 proxy with TLS, no-proxy host, unusable proxy text. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 4375e90e 4272a6b9 c4aff572 612192e3.
- 2026-10-16: Doing -> Done. A CURLOPT_CONNECT_ONLY 1 perform dials through the proxy EasyProxyOptions chooses, honouring the no-proxy list
