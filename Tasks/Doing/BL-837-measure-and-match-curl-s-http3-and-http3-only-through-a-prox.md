---
id: BL-837
title: Measure and match curl's --http3 and --http3-only through a proxy
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-731]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-837 — Measure and match curl's --http3 and --http3-only through a proxy

## Goal

An `https://` transfer with `--http3` or `--http3-only` and a proxy (`-x http://…`, `-x https://…`, `-x socks5://…`, `-x socks5h://…`) does exactly what curl.se's ngtcp2 build does, pinned from measurement: the exit code, the stderr text, the bytes the proxy receives, and whether the transfer continues over TCP.

## Context

- Today `HttpProtocolHandler.TriesQuic` (`Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`) returns false whenever `plan.Options.ForwardProxy` is not null, so `--http3` and `--http3-only` through any proxy connect over TCP exactly as without them. BL-731 left this unmeasured on purpose (ADR-0172, `Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md`, written by BL-731). `--http3-only` succeeding over TCP is certainly wrong: it must never speak anything but HTTP/3.
- What curl's source says, at tag `curl-8_18_0` (the source of curl.se's Windows build 8.18.0 with ngtcp2 1.21.0 and nghttp3 1.15.0, the HTTP/3 reference in ADR-0144), checked 2026-09-28: `Curl_conn_may_http3` in `lib/vquic/vquic.c` fails with exit 3 `CURLE_URL_MALFORMAT` and `HTTP/3 is not supported over a SOCKS proxy` when a SOCKS proxy is set, and with exit 3 and `HTTP/3 is not supported over an HTTP proxy` when an HTTP proxy tunnels (`conn->bits.httpproxy && conn->bits.tunnel_proxy`, always the case for an `https://` URL). `lib/cf-https-connect.c` calls it when choosing ALPNs: when HTTP/3 is only preferred (`--http3`) h3 is dropped and the h2/h1 attempt runs; when HTTP/3 is the only wanted version (`--http3-only`) the connect errors out. Newer curl (the `lib/vquic/cf-ngtcp2-proxy.c` on master, after 8.22.0) adds HTTP/3 through proxies; that is not the reference build and is out of scope.
- The source is a hypothesis; the measurement decides. Measure with `Record-CurlExchange.ps1` acting as the proxy (it records the request bytes, stdout, stderr and exit code), passing curl.se's build with `-Curl` (WinGet package `cURL.cURL`; its path is in ADR-0144's measurement table). Cases: `--http3` and `--http3-only`, each with `-x http://127.0.0.1:<port>`, `-x https://127.0.0.1:<port>` (with `--proxy-insecure`), `-x socks5://127.0.0.1:<port>` and `-x socks5h://127.0.0.1:<port>`, against `https://example.test/`; also `--http3-only` with `-x http://…` and an `http://` URL (which error wins, `non-HTTPS URL` or the proxy one), and `-v` for one `--http3` case to see whether the proxy refusal line is printed. If a proxy type needs a server the recorder cannot play, extend `Record-CurlExchange.ps1` rather than writing a new server; that script is then added to `touches` by editing this task before starting.
- Where the measurement shows `--http3-only` failing before connecting, the handler fails like `Http3NeedsHttps` does today (exit reported, `FailedConnectReport`, the message through `plan.Context.Events.ReportInfo`) without calling the connector.

## Acceptance criteria

- [ ] The measured runs (command line, exit code, stderr, proxy request bytes) for every case above are copied into this task's Notes, with the curl version line of the build measured.
- [ ] `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.Http3.cs` (or a new `HttpProtocolHandlerTests.Http3Proxy.cs`) pins each measured case: for each proxy kind, `--http3-only` returns the measured `CurlExitCode` and message and never calls the connector (or calls it as measured), and `--http3` connects through the proxy over TCP and never calls `ConnectMultiplexedAsync` (or does what was measured).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- ADR-0187 (BL-839) makes `curl-8_21_0` the HTTP/3 reference: there `Curl_conn_may_http3` still refuses a SOCKS proxy (exit 3, `HTTP/3 is not supported over a SOCKS proxy`) but no longer refuses a tunnelling HTTP proxy (8.18.0 did, exit 3, `HTTP/3 is not supported over an HTTP proxy`). Match 8.21.0.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
