---
id: BL-5288
title: Offer h2 by ALPN on https by default
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/HttpVersionMapping.cs, Curl.Cli.UnitLibrary/CurlVersionText.cs, Curl.Engine.UnitLibrary/ManagedCurlVersion.cs, Curl.Engine.UnitTests/ManagedCurlVersionTests.cs, Curl.Console.UnitTests/HttpVersionMappingTests.cs, Curl.Cli.UnitTests/CurlVersionTextTests.cs, Libcurl.UnitTests/Misc/Version/CurlVersionTests.cs]
requirement: none
created: 2026-11-16
completed: 2026-11-16
---
# BL-5288 — Offer h2 by ALPN on https by default

## Goal

On an `https://` URL with no HTTP version option, the port offers `h2,http/1.1` in the TLS ALPN extension and speaks HTTP/2 when the server accepts `h2`, as upstream curl 8.21.0 does by default.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 11 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http2.md`, finding 1. Related: BL-5290 (handoff entry 13), which depends on this one.
- **Surl tests (9):** every `https` case of `CurlPortBesideUpstreamCurlOverHttp2Tests`, e.g. `Get_HttpsByAlpn_IsServedOverHttp2`, `Head_HttpsByAlpn_ExitsZeroWithAnHttp2OkHead`, `Get_TwoUrls_ReuseOneHttp2Connection` (all listed in `Http2.md`); Surl commit `6268bee500368fa80fd2284c689e5e40b15301a0`.
- **Command line, e.g.:** `curl -sS -k -w %{http_version} https://127.0.0.1:<port>/hello.txt` against `surl --self-signed https://127.0.0.1:0/`.
- **Upstream curl 8.21.0 (correct):** static OpenSSL build, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`: exit 0; `-w %{http_version}` writes `2`; `-I` line 1 `HTTP/2 200 `; `-v` shows `* ALPN: curl offers h2,http/1.1`, `* ALPN: server accepted h2`.
- **Port (defect):** exit 0 with the same bodies; writes `1.1`; `-I` line 1 `HTTP/1.1 200 OK`; `-v` shows `* ALPN: curl offers http/1.1`. The port's `-V` lists no `HTTP2` feature. Its `h2c` cases on `http` agree with upstream, so its HTTP/2 framing is not at fault.
- **What decides:** curl 8.21.0 manual, `--http2`: "For HTTPS, this means curl negotiates HTTP/2 in the TLS handshake. curl does this by default" (https://curl.se/docs/manpage.html); RFC 9113 section 3.2; RFC 7301.
- **Where to start:** `Curl.Engine.UnitLibrary/HttpVersionMapping.cs`, `DefaultOffer`: today `http/1.1` alone on Windows, as the Schannel reference build offers (ADR-0141), and `h2,http/1.1` elsewhere.

## Acceptance criteria

- [x] `curl -k -w %{http_version} https://<h2 server>/` writes `2`, and `-v` shows `* ALPN: curl offers h2,http/1.1`, shown by port tests.
- [x] `--http1.1` still offers only `http/1.1`, and `curl -V` lists `HTTP2` in `Features:`.

## Notes

ADR-1274 amends ADR-0141: no version option offers `h2,http/1.1` on every platform, and the Windows `-V` Features line (and ManagedCurlVersion) lists `HTTP2`. The ALPN criteria are shown by `HttpVersionMappingTests` (data rows and `CreateTcpConnector_NoVersionOption_OffersH2ThenHttp11`); `--http1.1` rows still pin `http/1.1`. The Engine-side `HttpVersionMappingDefaultOfferTests` was not added: Engine.UnitTests has no InternalsVisibleTo access, so the Console test project covers it. Touches widened to the test and version files the change pins. Platform-neutral. Quality run on Curl.Engine.UnitLibrary: 0 failing.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 0e62be3f 7a2a3b56.
- 2026-11-16: Doing -> Done. https offers h2,http/1.1 by default on every platform; Windows -V lists HTTP2
