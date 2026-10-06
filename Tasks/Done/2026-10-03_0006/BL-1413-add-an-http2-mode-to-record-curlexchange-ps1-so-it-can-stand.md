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
completed: 2026-10-03
---
# BL-1413 — Add an -Http2 mode to Record-CurlExchange.ps1 so it can stand in for an HTTP/2 HTTPS proxy (ADR-0408)

## Goal

`Record-CurlExchange.ps1 -Tls -Http2` stands in for an HTTPS proxy that picks `h2` by ALPN. It answers an HTTP/2 `CONNECT` stream with a scripted status and records the HEADERS and DATA frames curl sends.

## Context

- ADR-0408 decision 6: the `-v` lines of an HTTP/2 proxy tunnel must be measured before BL-1414 pins them. The recorder cannot speak HTTP/2 today.
- Its `-Tls` switch (BL-442) already runs the TLS side. This task adds HTTP/2 framing in PowerShell: the preface, SETTINGS, HPACK decoding of the request headers, a HEADERS reply and DATA relay. Replies may use HPACK literals without indexing.
- Measure with real curl 8.18.0 OpenSSL with nghttp2 under WSL, using `-ListenAddress` as ADR-0190 did.

## Acceptance criteria

- [x] `Record-CurlExchange.ps1 -Tls -Http2 -ProxyStatus 200`, run against WSL curl `-v --proxy-insecure --proxy-http2 -x https://<host>:<port> http://example.test/`, records the decoded CONNECT pseudo-headers and curl's stdout, stderr and exit code.
- [x] The same run with `-ProxyStatus 407`, and with the recorder picking `http/1.1` by ALPN, both complete and record curl's output.
- [x] The script's help documents `-Http2` and its parameters.
- [x] The measured `-v` lines for the 200, 407 and `http/1.1` fallback cases are pasted into this task's Notes for BL-1414.

## Notes

- New parameters: `-Http2` (needs `-Tls`), `-ProxyStatus` (default 200) and `-ProxyAlpn`
  (`h2` default, or `http/1.1` for the fallback). The answer to a 2xx CONNECT relays the
  first `-Response` as one DATA frame ending the stream; other statuses, and non-CONNECT
  requests, get `content-length: 0` and END_STREAM.
- Choice: the HTTP/2 side is a C# class compiled with Add-Type inside the script, as the
  DNS and TFTP responders already are, rather than PowerShell functions: it runs on its own
  thread with no runspace. Still a PowerShell script, no Python, no package.
- Choice: `-Http2` needs PowerShell 7 (`pwsh`). Server-side ALPN needs
  `SslServerAuthenticationOptions.ApplicationProtocols`, which .NET Framework (Windows
  PowerShell 5.1) lacks; 5.1 gets a clear refusal, every other mode is unchanged there.
- Choice: HPACK Huffman strings are decoded by .NET's internal
  `System.Net.Http.HPack.Huffman.Decode`, bound by reflection to a delegate, instead of
  copying RFC 7541's 257-code table by hand (a mistyped code would silently misdecode).
  The static table's 61 names are written out. Replies use literals without indexing.
- Learned: `http://example.test/` through `--proxy-http2` without `-p` is *forwarded* as an
  HTTP/2 GET (`:method GET`, `:scheme http`, `:path /`), not tunnelled; the CONNECT the
  task wants needs `-p` (or an https:// target). The measurements below use `-p`.
- Measured with WSL Ubuntu curl 8.18.0 (OpenSSL/3.5.5, nghttp2/1.68.0), recorder on
  `-ListenAddress 172.26.96.1 -Port 18443`, curl
  `-v --proxy-insecure --proxy-http2 -p -x https://172.26.96.1:18443 http://example.test/`,
  `-Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello'`.
  TLS handshake, certificate detail, `Trying`/`Established connection` and progress-meter
  lines are left out below.

  Decoded h2 CONNECT (transcript.txt): `SETTINGS 3=100 4=10485760 2=0`,
  `WINDOW_UPDATE stream 0 increment 104792065`, then HEADERS stream 1 flags 0x04
  (END_HEADERS, no END_STREAM) holding exactly `:method: CONNECT`,
  `:authority: example.test:80`, `user-agent: curl/8.18.0` - no `:scheme`, no `:path`.
  The tunnelled request arrives as one DATA frame, and curl ends with GOAWAY last stream 0
  error 0.

  **200 over h2** - exit 0, stdout `hello`:
  ```
  * ALPN: curl offers h2,http/1.1
  * SSL Trust: peer verification disabled
  * ALPN: server accepted h2
  * Proxy certificate:
  *  SSL certificate verification failed, continuing anyway!
  * CONNECT: 'h2' negotiated
  * Establish HTTP/2 proxy tunnel to example.test:80
  * CONNECT tunnel established, response 200
  * CONNECT phase completed
  * using HTTP/1.x
  > GET / HTTP/1.1
  > Host: example.test
  > User-Agent: curl/8.18.0
  > Accept: */*
  >
  * Request completely sent off
  < HTTP/1.1 200 OK
  < Content-Length: 5
  <
  * Connection #0 to host 172.26.96.1:18443 left intact
  ```

  **407 over h2** (HEADERS `:status 407`, `content-length 0`, END_STREAM) - exit 7,
  stdout empty, and curl prints no `< ` line for the proxy's answer:
  ```
  * ALPN: curl offers h2,http/1.1
  * SSL Trust: peer verification disabled
  * ALPN: server accepted h2
  * Proxy certificate:
  *  SSL certificate verification failed, continuing anyway!
  * CONNECT: 'h2' negotiated
  * Establish HTTP/2 proxy tunnel to example.test:80
  * closing connection #0
  curl: (7) Could not connect to server
  ```

  **200 with the recorder picking http/1.1 by ALPN** - exit 0, stdout `hello`:
  ```
  * ALPN: curl offers h2,http/1.1
  * SSL Trust: peer verification disabled
  * ALPN: server accepted http/1.1
  * Proxy certificate:
  *  SSL certificate verification failed, continuing anyway!
  * CONNECT: 'http/1.1' negotiated
  * allocate connect buffer
  * Establish HTTP proxy tunnel to example.test:80
  > CONNECT example.test:80 HTTP/1.1
  > Host: example.test:80
  > User-Agent: curl/8.18.0
  > Proxy-Connection: Keep-Alive
  >
  < HTTP/1.1 200 Connection established
  <
  * CONNECT phase completed
  * CONNECT tunnel established, response 200
  > GET / HTTP/1.1
  > Host: example.test
  > User-Agent: curl/8.18.0
  > Accept: */*
  >
  * Request completely sent off
  < HTTP/1.1 200 OK
  < Content-Length: 5
  <
  * Connection #0 to host 172.26.96.1:18443 left intact
  ```
  Note the order differs from h2: over http/1.1 "CONNECT phase completed" comes before
  "CONNECT tunnel established, response 200"; over h2 it comes after. The `>`/`<` blank
  lines are "> " and "< " with a trailing space in curl's output.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Record-CurlExchange.ps1 -Tls -Http2 stands in for an h2 HTTPS proxy; 200, 407 and http/1.1-fallback -v lines measured for BL-1414
