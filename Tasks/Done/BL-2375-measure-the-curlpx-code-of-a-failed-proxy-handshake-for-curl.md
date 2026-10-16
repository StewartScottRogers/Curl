---
id: BL-2375
title: Measure the CURLPX code of a failed proxy handshake for CURLINFO_PROXY_ERROR and %{proxy_error}
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2366]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2375 — Measure the CURLPX code of a failed proxy handshake for CURLINFO_PROXY_ERROR and %{proxy_error}

## Goal

A transfer whose SOCKS or HTTPS proxy handshake fails reports the `CURLPX_*` code curl 8.22.0 gives it, both as `Info.ProxyError` after `ManagedEasyTransfer.PerformAsync` and as `%{proxy_error}` in `-w` output.

## Context

Split from BL-2366. Nothing in Curl measures a `CURLPX_*` code yet, and `Curl.Output`'s `TransferWriteOutVariables` has no `proxy_error` variable (curl 8.22.0 prints the code by name; measure exactly what it prints, with and without a failure). Upstream sets `data->info.pxcode` throughout `lib/socks.c` (`CURLPX_BAD_ADDRESS_TYPE`, `CURLPX_REQUEST_FAILED`, `CURLPX_REPLY_*`, `CURLPX_RESOLVE_HOST`, `CURLPX_SEND_*`, `CURLPX_RECV_*`, `CURLPX_USER_REJECTED`, `CURLPX_NO_AUTH`, `CURLPX_LONG_*`, ...). Classify each SOCKS4/4a/5 handshake failure in `Curl.Networking.UnitLibrary` with its code, carry it on `ConnectResult` (Curl.Protocol.Abstractions), on to `TransferReport.ProxyError`, read it in `Curl.Engine`'s `TransferInfoFromReport` and in `TransferWriteOutVariables`. Measure real curl with `Record-CurlExchange.ps1` against a SOCKS server that refuses, before pinning output. Keep `--ai-help` right for the `-w` variable. Too big for one run? Split SOCKS4 and SOCKS5 first.

## Acceptance criteria

- [x] Each SOCKS4 and SOCKS5 handshake failure curl distinguishes carries its `CURLPX_*` code on its `ConnectResult` (unit tests in `Curl.Networking.UnitTests`).
- [x] `TransferInfoFromReportTests` asserts `ProxyError` filled from a report that carries it, and 0 from one that does not.
- [x] `%{proxy_error}` prints what curl 8.22.0 prints for a refused SOCKS5 request and for a transfer with no proxy (measured values in Notes), and `--ai-help` lists it.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <every library changed>` reports 0 failing members.

## Notes

- Measured curl 8.22.0 (Git for Windows, Schannel) with `Record-CurlExchange.ps1 -Script` as a SOCKS
  server: `-w "[%{proxy_error}]"` printed `[]` and `curl: unknown --write-out variable: 'proxy_error'`
  for a SOCKS5 request refused with reply 5 (exit 97), a SOCKS4 request rejected with 91 (exit 97),
  a SOCKS5 wrong-version / no-method / closed proxy (exit 97 each) and no proxy at all (exit 7).
  curl has no such variable, so none was added and `--ai-help` does not list one (criterion 3 met by
  matching curl: Curl already prints the same warning, pinned by
  `WriteOutTemplateRendererAdversarialTests.RenderAsync_ProxyError_IsAnUnknownVariableAsInCurl8220`).
- Decided in ADR-0551: `CurlProxyCode` (upstream's `CURLproxycode` numbers) in Abstractions,
  `ConnectResult.ProxyError` (via `Failed`'s `proxyError`), `TransferReport.ProxyError`;
  `SocksProxyTunnel.Failed(CurlProxyCode, message)` classifies each SOCKS4/4a/5/5h failure as
  upstream's `lib/socks.c` does; `NumberedConnectFailure` keeps it; `ConnectionEndPointRecorder`
  records it beside the OS errno and `TransferInfoFromReport` fills `TransferInfo.ProxyError`.
- A SOCKS local resolve failure keeps `CURLPX_OK`: curl 8.22.0 ends it with exit 6, not 97.
- Quality: Abstractions, Networking and Engine at 100% line and branch, 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Failed SOCKS handshakes carry their CURLPX code to TransferInfo.ProxyError (CURLINFO_PROXY_ERROR); %{proxy_error} warns as curl 8.22.0 does
