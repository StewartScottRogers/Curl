---
id: BL-5290
title: Negotiate HTTP/2 over https when the server also advertises h3
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-5288]
touches: [Curl.Engine.UnitTests/HttpsH2WithAltSvcH3Tests.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5290 — Negotiate HTTP/2 over https when the server also advertises h3

## Goal

`curl -k -I https://...` against a server that also listens for HTTP/3 gets an `HTTP/2 200` head carrying `alt-svc: h3=":<port>"`, as upstream curl 8.21.0 does; the same defect as BL-5288, recorded in the HTTP/3 family because its mirrored test pins the `HTTP/2 200` line.

## Context

Filed from Surl's Phase 7 measurement of the port (Stewart approved filing on the port's board, 2026-11-16): Surl `Documentation/Conformance/CurlPort/Port-Board-Handoff.md`, entry 13 (github.com/StewartScottRogers/Surl). The port was measured at commit `b42c319ba940f66452b2b6f3e9910d46825b08fe` (port `curl.exe` SHA-256 `F28D1417807AA862BA2A6D5B0BF0CF1200673BE45B959B1A78E1E4B476EF8FA7`), on Windows x64, 2026-11-05, beside upstream curl 8.21.0, the oracle: where the two differ against the same Surl server, the port is the one in the wrong.

- **Finding:** Surl `Documentation/Conformance/CurlPort/Http3.md`, finding 2. Fixing BL-5288 is expected to fix this one, so this task depends on it and only pins the result.
- **Surl test:** `CurlPortBesideUpstreamCurlOverHttp3Tests.Head_OverTcpWithHttp3On_CarriesAltSvcNamingThePort`, Surl commit `883c3d4d4881bb59b9179bd1cb3f33973096af7f`.
- **Command line:** `curl -sS -k -I https://127.0.0.1:<port>/hello.txt` against `surl --http3`.
- **Upstream curl 8.21.0 (correct):** static OpenSSL build, SHA-256 `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648`: exit 0; line 1 `HTTP/2 200 `, lower-case fields ending `alt-svc: h3=":<port>"; ma=86400`.
- **Port (defect):** exit 0; line 1 `HTTP/1.1 200 OK`, then `Date:` ... `Alt-Svc: h3=":<port>"; ma=86400`.
- **What decides:** curl 8.21.0 manual, `--http2` ("curl does this by default"); RFC 9113 section 3.2; RFC 7301.

## Acceptance criteria

- [ ] `curl -k -I https://<server with h2 and h3>/` writes `HTTP/2 200 ` as its first line, with the `alt-svc` field in lower case, shown by a port test.

## Notes

## Log

- 2026-11-16: Created.
