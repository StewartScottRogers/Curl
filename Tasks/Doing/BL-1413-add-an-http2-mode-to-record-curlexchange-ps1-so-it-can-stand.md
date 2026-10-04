---
id: BL-1413
title: Add an -Http2 mode to Record-CurlExchange.ps1 so it can stand in for an HTTP/2 HTTPS proxy (ADR-0408)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-10-03
completed:
---
# BL-1413 — Add an -Http2 mode to Record-CurlExchange.ps1 so it can stand in for an HTTP/2 HTTPS proxy (ADR-0408)

## Goal

`Record-CurlExchange.ps1 -Tls -Http2` stands in for an HTTPS proxy that picks `h2` by ALPN. It answers an HTTP/2 `CONNECT` stream with a scripted status and records the HEADERS and DATA frames curl sends.

## Context

- ADR-0408 decision 6: the `-v` lines of an HTTP/2 proxy tunnel must be measured before BL-1414 pins them. The recorder cannot speak HTTP/2 today.
- Its `-Tls` switch (BL-442) already runs the TLS side. This task adds HTTP/2 framing in PowerShell: the preface, SETTINGS, HPACK decoding of the request headers, a HEADERS reply and DATA relay. Replies may use HPACK literals without indexing.
- Measure with real curl 8.18.0 OpenSSL with nghttp2 under WSL, using `-ListenAddress` as ADR-0190 did.

## Acceptance criteria

- [ ] `Record-CurlExchange.ps1 -Tls -Http2 -ProxyStatus 200`, run against WSL curl `-v --proxy-insecure --proxy-http2 -x https://<host>:<port> http://example.test/`, records the decoded CONNECT pseudo-headers and curl's stdout, stderr and exit code.
- [ ] The same run with `-ProxyStatus 407`, and with the recorder picking `http/1.1` by ALPN, both complete and record curl's output.
- [ ] The script's help documents `-Http2` and its parameters.
- [ ] The measured `-v` lines for the 200, 407 and `http/1.1` fallback cases are pasted into this task's Notes for BL-1414.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
