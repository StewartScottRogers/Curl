---
id: BL-2472
title: Engine: CURLOPT_CONNECT_ONLY perform through a proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2423]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2472 — Engine: CURLOPT_CONNECT_ONLY perform through a proxy

## Goal

A `CURLOPT_CONNECT_ONLY` 1 perform of the managed engine with a proxy set (HTTP `CONNECT` tunnel, SOCKS4/5, HTTPS proxy) connects through it, as libcurl does, before `curl_easy_recv` and `curl_easy_send` use the connection.

## Context

ADR-0608 (BL-2423): `ManagedEasyTransfer.PerformConnectOnlyAsync` dials `ManagedCurlEngine.CreateConnectOnlyConnector`'s `TcpConnector` with a bare `ConnectTarget` (host, port, TLS) and sets no `ConnectTarget.Proxy`, so a proxy option is ignored. Choose the proxy as a normal perform does (`TransferProxySelection.TrySelect` over `EasyProxyOptions`, honouring the no-proxy list) and put it on the target; libcurl tunnels through an HTTP proxy for connect-only even without `CURLOPT_HTTPPROXYTUNNEL`.

## Acceptance criteria

- [ ] A `ManagedEasyTransferTests` test shows a connect-only perform with `EasyProxyOptions.Proxy` set dials a `ConnectTarget` whose `Proxy` names that proxy, and one with the host in the no-proxy list dials none.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members.
- [ ] ADR-0608 and `Curl.Engine.UnitLibrary/CLAUDE.md` say the proxy is used.

## Notes

## Log

- 2026-10-16: Created.
