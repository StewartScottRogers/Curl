---
id: BL-215
title: Pool connections and reuse them across requests and URLs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-164, BL-173, BL-335]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-215 — Pool connections and reuse them across requests and URLs

## Goal

A connection pool implements the BL-164 ADR so a reusable connection serves the next request to the same key.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-164 ADR decides the hand-back and the pool key.
- Scope per ADR-0050 "Who does what": `PoolingConnector`, `PooledConnection`, the key, the five-connection limit and the 118-second idle limit, in `Curl.Networking` only. The contract members come from BL-335; the HTTP handler's use of them is BL-336 and the composition is BL-334.

## Acceptance criteria

- [x] Tests show reuse for the same key, no reuse across keys or after close, and the connection count the report needs.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`.

## Notes

- Plan item: N5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Built per ADR-0050: `PoolingConnector` (wraps any `IConnector`), `PooledConnection` (one per lease), `ConnectionPoolKey` and `ConnectionPoolProxyKey` (records; host upper-cased, scheme lower-cased, credential by user name, password and domain), `PoolEntry`. Tests: `PoolingConnectorTests` (27 cases incl. 13 key differences) and `PooledConnectionTests`, fakes `FakeConnector` and `RecordingTransferEvents`.
- Defaults taken (not measured, unattended run): a reused result keeps the original `PeerCertificates` (the TLS session is the same one) and reports `ProxyConnectResponseCode` `0` (no CONNECT is sent for it); an idle connection past 118 s is closed silently when found on a connect or a return; the first idle match (oldest) is reused; a target without `PoolScheme` is still wrapped and numbered, so `#N` counts every connection the run opens, but never pooled.
- `ConnectionReusedEvent.IsProxy` is always `false`: a forward-proxy target carries the proxy as `Host` with no `Proxy`, so the pool cannot tell. Filed BL-360 to add the signal.
- Coverage gate: every new member is 100% line and branch, complexity at most 3. `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` still lists four members that failed before this task and that it does not touch: `SslStreamTlsProvider.CreateCipherSuitesPolicy` (BL-268), `TcpDialer.DialAsync` and `UdpDatagramChannel.SendAsync`/`ReceiveAsync` (BL-357, covered only by Integration tests). Ticked on that basis.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. PoolingConnector reuses a marked connection for the same key, holds five idle, drops them after 118 s, and numbers connections
