---
id: BL-1857
title: Fail a CONNECT reply as curl does: Invalid response header (43), the refused code in http_connect, a malformed chunked 407 body (56)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed:
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

- [ ] Upstream test750 passes and is listed in `PassingUpstreamCases.txt`.
- [ ] Upstream test217 passes and is listed in `PassingUpstreamCases.txt`.
- [ ] Upstream test1715 passes and is listed in `PassingUpstreamCases.txt`.
- [ ] `Curl.Networking.UnitTests` pins each of the three behaviours with a unit test.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
