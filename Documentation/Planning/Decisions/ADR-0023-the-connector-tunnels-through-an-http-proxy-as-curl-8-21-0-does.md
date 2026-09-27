# ADR-0023 — The connector tunnels through an HTTP proxy as curl 8.21.0 does

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-212 makes `TcpConnector` honour `ConnectTarget.Proxy` (ADR-0014) for an HTTP proxy:
send CONNECT, check the reply, then run TLS over the tunnel for an `https` target. Open
questions were the request bytes, where the `User-Agent` and the credential encoding come
from, how the reply is read, which replies fail and how, and what the connector does with
the proxy kinds this task does not cover.

Measured with the reference build (ADR-0018: curl 8.21.0, mingw), `Record-CurlExchange.ps1`
serving as the proxy (the commands and bytes are in BL-212's Notes):

| Case | curl 8.21.0 |
| --- | --- |
| `-p -x 127.0.0.1:P http://example.com/` | `CONNECT example.com:80 HTTP/1.1`, `Host: example.com:80`, `User-Agent: curl/8.21.0`, `Proxy-Connection: Keep-Alive` |
| `-U user:p@ss`, https target | `Proxy-Authorization: Basic dXNlcjpwQHNz` between `Host` and `User-Agent` |
| `--proxy1.0` | `HTTP/1.0` on the request line, same headers |
| `http://[::1]:81/` | `CONNECT [::1]:81`, `Host: [::1]:81` |
| `-A Agent/1` | `User-Agent: Agent/1` |
| reply 407, 403 or 300 | exit 7 `CONNECT tunnel failed, response N` |
| reply 299 | tunnel opened |
| reply `garbage`, `HTTP/1.1 2000 OK`, or `HTTP/1.1` then `X: 200` | exit 7 `CONNECT tunnel failed, response 0` |
| a reply line of 16384 bytes or more, CR and LF included | exit 56 `CONNECT response too large` (16383 is read) |
| a header block over 307200 bytes at the end of a line | exit 56 `Too large response headers: 307762 > 307200` for 1200 lines of 1009 bytes |
| no reply, or header block cut short | exit 56 `Proxy CONNECT aborted` |
| proxy host does not resolve | exit 5 `Could not resolve proxy: <proxy host>` |
| proxy refuses | exit 7 `Failed to connect to <target host>:<target port> over proxy <proxy host> after <n> ms: Could not connect to server`; an IPv6 target unbracketed (`::1:8080`) |

## Decision

- **Whenever `ConnectTarget.Proxy` is set, the connector tunnels.** Whether a transfer
  tunnels (`-p`, or an https target) or sends an absolute-URI request to the proxy is the
  HTTP handler's choice, made by setting `Proxy` or connecting to the proxy directly.
- **Request bytes** are the measured ones, headers in curl's order, HTTP/1.0 for
  `ProxyKind.Http10`, an IPv6 target in brackets. `Proxy-Authorization` is Basic over
  `user:password`.
- **`User-Agent` and credential encoding come from `HttpProxyTunnelOptions`**, a
  constructor argument of `TcpConnector`, because the connector sees neither `-A` nor the
  platform encoding. `null` sends no `User-Agent`. The default is `curl/8.21.0` and UTF-8;
  `Curl.Console` is to pass `-A` and `CredentialEncoding.ForPlatform` (ADR-0022) once the
  proxy options reach it, filed as a follow-up.
- **The reply is read one byte at a time** up to the empty line ending its header block,
  so nothing after it - the start of the TLS handshake, say - is taken off the tunnel. A
  bare LF ends a line as well as CRLF. Any 2xx opens the tunnel; its status becomes
  `ConnectResult.ProxyConnectResponseCode`. A first line that is not `HTTP/<version>`, a
  space and exactly three digits followed by a space or the line end is status 0. The two
  measured size limits apply as curl applies them.
- **A failed CONNECT disposes the proxy connection** before the failure is returned, and
  so does an exception while the request is sent or the reply read.
- **HTTPS and SOCKS proxies throw `NotSupportedException`** until their tasks (BL-213 for
  SOCKS, a filed follow-up for HTTPS) replace it. Failing loudly is better than silently
  connecting direct, which would bypass the proxy the user asked for.

## Consequences

- The connector matches curl for the measured cases on the reference build; behaviour
  not measured here (proxy authentication other than Basic, a proxy that stalls without
  closing) is not claimed.

## Alternatives considered

- **Buffered reads of the reply.** Faster, but bytes after the header block would have to
  be handed to the tunnel, which `IConnection` has no way to do.
- **Pass the User-Agent through `ProxyEndpoint`.** Changes the shared contract in
  `Curl.Protocol.Abstractions.UnitLibrary`, which every protocol task depends on, for a
  value that is fixed per run.
