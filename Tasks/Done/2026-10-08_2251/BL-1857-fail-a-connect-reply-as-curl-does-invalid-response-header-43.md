---
id: BL-1857
title: Fail a CONNECT reply as curl does: Invalid response header (43), the refused code in http_connect, a malformed chunked 407 body (56)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Conformance.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1857 — Fail a CONNECT reply as curl does: Invalid response header (43), the refused code in http_connect, a malformed chunked 407 body (56)

## Goal

Upstream test217, test750 and test1715 pass through the in-process harness in `UpstreamConformanceTests` and are listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-1856. Measured 2026-10-08 (unlisted, so Inconclusive in the run):
- test750: the proxy answers CONNECT with `<html>` lines and no status line. Expected exit 43 `curl: (43) Invalid response header`, got `curl: (56) Proxy CONNECT aborted`. Curl reads to the empty line in `HttpProxyTunnel.ReadReplyAsync` (`Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`) and the reply never has one; curl fails once the first line ends and does not start with `HTTP/`. A first line starting `HTTP/` with a malformed code stays status 0 (pinned by existing tests).
- test217: `405` reply then close, `-w "%{http_code} %{http_connect}\n"`: expected `000 405`, got `000 000` (exit 7 already matches). `TcpConnector.TunnelFailure` (`TcpConnector.cs`) returns `ConnectResult.Failed` without the reply's status, so the console never sees `ProxyConnectResponseCode`.
- test1715: `407` with `Content-Length: 13` and `Transfer-Encoding: chunked`, body `some content`, no `--proxy-user`: expected exit 56, got 7. curl reads the chunked body and its malformed chunk is exit 56 (`DiscardChunkedBodyAsync`); Curl fails with 7 before reading it when no credential answers the 407.

## Acceptance criteria

- [x] Upstream test750 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test217 passes and is listed in `PassingUpstreamCases.txt`.
- [x] Upstream test1715 passes and is listed in `PassingUpstreamCases.txt`.
- [x] `Curl.Networking.UnitTests` pins each of the three behaviours with a unit test.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- test217 needed the CONNECT status on a failed connect: `ConnectResult.Failed` took an optional `proxyConnectResponseCode` (Curl.Protocol.Abstractions.UnitLibrary) and `HttpProtocolHandler.FailedConnectReport` copies it (Curl.Protocol.Http.UnitLibrary). Both added to `touches`; no task in Doing on origin/work/dark-factory named them.
- test750: a first line not starting `HTTP/` is now exit 43 `Invalid response header` as soon as it ends; the old `garbage` and `FTP/1.1 200 OK` rows that pinned status 0 were removed (curl measured by upstream test750). A first line starting `HTTP/` with a malformed code still stays status 0.
- test1715: an unanswered 407 with a chunked body has that body read and discarded first, so a malformed chunk is exit 56; a body read to its end still leaves exit 7.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Upstream test217, test750 and test1715 pass and are listed; unit tests pin all three.
