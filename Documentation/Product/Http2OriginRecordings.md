# HTTP/2 origin recordings (BL-3209)

What an nghttp2-built curl 8.22.0 sends and prints when it talks to an HTTP/2 origin, recorded
for BL-3207 to pin Curl against. Every `-v` line, request byte and exit code below was measured,
not assumed.

## How they were recorded

- Curl build: curl 8.22.0_1 from curl.se/windows (`curl-8.22.0_1-win64-mingw.zip`, released
  2026-09-02): `curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 LibreSSL/4.3.2 ... nghttp2/1.70.0`.
  It was unzipped into its own empty directory and passed with `-Curl`.
- Origin: `Record-CurlExchange.ps1 -Http2Origin <mode>` (PowerShell 7). `Response` was
  `HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello`. An HTTP/1.1 answer sends that; an HTTP/2
  answer is a HEADERS frame `:status: 200`, `content-length: 0` that ends the stream (no body).
- Every command line also had `-sS -v -w '\nHTTPV=%{http_version}\n'` and ran against
  `127.0.0.1`. TLS cases add `-k` (TLS 1.2, throwaway certificate).
- `request.bin` holds the bytes below; the HTTP/1.1 requests end in a blank line (`\r\n\r\n`).
- All five exit codes are 0.

## Results

| Mode | Command line (after the common flags) | `%{http_version}` | stdout body | Exit |
| --- | --- | --- | --- | --- |
| `h2c-upgrade` | `--http2 http://127.0.0.1:P/` | `2` | empty | 0 |
| `h2c-upgrade-ignored` | `--http2 http://127.0.0.1:P/` | `1.1` | `hello` | 0 |
| `prior-knowledge` | `--http2-prior-knowledge http://127.0.0.1:P/` | `2` | empty | 0 |
| `alpn`, ALPN `h2` | `--http2 -k https://127.0.0.1:P/` | `2` | empty | 0 |
| `alpn`, ALPN `http/1.1` | `--http2 -k https://127.0.0.1:P/` | `1.1` | `hello` | 0 |

## Request bytes

### `--http2` over h2c (upgrade accepted)

Curl first sends an HTTP/1.1 request asking for the upgrade (`\r\n` line ends):

```
GET / HTTP/1.1
Host: 127.0.0.1:P
User-Agent: curl/8.22.0
Accept: */*
Upgrade: h2c
HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA
Connection: Upgrade, HTTP2-Settings

```

After `101 Switching Protocols` it sends the preface `PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n`, then
frames. The upgraded request is stream 1 and curl sends no HEADERS frame for it:

```
> SETTINGS 3=100 4=65536 2=0
> WINDOW_UPDATE stream 0 increment 1048510465
> WINDOW_UPDATE stream 1 increment 10420225   (twice)
> SETTINGS ACK
> GOAWAY last stream 0 error 0
```

`-v` lines that differ from HTTP/1.1: `> Upgrade: h2c`, `> HTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA`,
`> Connection: Upgrade, HTTP2-Settings`, then `< HTTP/1.1 101 Switching Protocols`,
`< Connection: Upgrade`, `< Upgrade: h2c`, `* Received 101, Switching to HTTP/2`, and the
response is shown as `< HTTP/2 200 ` and `< content-length: 0`.

### `--http2` over h2c (upgrade ignored)

The same HTTP/1.1 request as above. The origin answers `HTTP/1.1 200 OK` and curl stays on
HTTP/1.1: `< HTTP/1.1 200 OK`, `%{http_version}` is `1.1`, exit 0, and no `Received 101` line.

### `--http2-prior-knowledge`

No HTTP/1.1 request. The connection opens with the preface, then:

```
> SETTINGS 3=100 4=65536 2=0
> WINDOW_UPDATE stream 0 increment 1048510465
> HEADERS stream 1 flags 0x05      (END_STREAM | END_HEADERS)
>   :method: GET
>   :scheme: http
>   :authority: 127.0.0.1:P
>   :path: /
>   user-agent: curl/8.22.0
>   accept: */*
> WINDOW_UPDATE stream 1 increment 10420225   (twice)
> SETTINGS ACK
> GOAWAY last stream 0 error 0
```

`-v` lines: `* [HTTP/2] [1] OPENED stream for http://127.0.0.1:P/`, one `* [HTTP/2] [1] [name: value]`
line per header above, `> GET / HTTP/2`, `> Host: 127.0.0.1:P`, `> User-Agent: curl/8.22.0`,
`> Accept: */*`, `< HTTP/2 200 `, `< content-length: 0`.

### `--http2` over TLS, ALPN `h2`

TLS 1.2 handshake with `* ALPN: curl offers h2,http/1.1` and `* ALPN: server accepted h2`; the
decrypted bytes are exactly the prior-knowledge frames above, with `:scheme: https` and
`* [HTTP/2] [1] OPENED stream for https://127.0.0.1:P/`.

### `--http2` over TLS, ALPN falls back to `http/1.1`

`* ALPN: curl offers h2,http/1.1` and `* ALPN: server accepted http/1.1`. The decrypted request
is plain HTTP/1.1 with no `Upgrade` or `HTTP2-Settings` header:

```
GET / HTTP/1.1
Host: 127.0.0.1:P
User-Agent: curl/8.22.0
Accept: */*

```

Response `< HTTP/1.1 200 OK`, `%{http_version}` `1.1`, exit 0.

## Platform note

These lines come from a LibreSSL build. Schannels other TLS lines differ from them (not measured here), so BL-3207 should pin only the ALPN, `HTTP/2`, `Upgrade` and frame lines.
