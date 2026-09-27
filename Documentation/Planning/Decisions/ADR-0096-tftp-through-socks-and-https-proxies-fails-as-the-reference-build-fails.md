# ADR-0096 — `tftp://` through a SOCKS or HTTPS proxy fails as the reference build fails

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"; recorded
  by BL-398, 2026-09-27). Extends ADR-0056, rule 4.

## Context

ADR-0056 rule 4 covered `tftp://` through an HTTP proxy only. With the ADR-0053 guard gone
(BL-328), a SOCKS or HTTPS proxy reached `TftpProtocolHandler`, which opened the datagram
channel straight to the server: TFTP traffic went around a proxy the user named.

BL-398 measured curl 8.21.0 (the mingw Schannel build, ADR-0009) against loopback
listeners; the commands and bytes are in BL-398's `Notes`. In short:

| Command | What the proxy received | Result |
| --- | --- | --- |
| `--socks5`, `--socks4`, `--socks4a`, `--socks5-hostname`, `-x socks5://…` | nothing | exit 97 `Send failure: Socket is not connected` |
| the same with `tftp://example.com/` (no file name) | nothing | exit 97, the same |
| `-x https://p` (TLS proxy) answering `200`, `204` or `101` | TLS handshake, then `GET https://p/.well-known/masque/udp/example.com/69/` with `Upgrade: connect-udp` | exit 7 `bind() failed; Invalid arguments` |
| `-x https://p` or `-x http://p` answering `403` / `407` / `302` | the same request | exit 7 `CONNECT-UDP tunnel failed, response 403` (…`407`, …`302`) |
| `-x http://p` answering `garbage` | the same request | exit 7 `CONNECT-UDP tunnel failed, response 0` |
| `-x http://p` closing without a reply | the same request | exit 56 `Proxy CONNECT aborted` |
| `-x https://p` failing the TLS handshake | a ClientHello | exit 35 `schannel: failed to receive handshake, SSL/TLS connection failed` |

libcurl on Windows sends the SOCKS greeting on the transfer's unconnected UDP socket, so the
send fails before any byte leaves; through an HTTP or HTTPS proxy it reads the reply to the
MASQUE request, and only a success reply reaches the `bind()` that fails.

## Decision

1. **SOCKS:** any SOCKS `ProxyKind` ends the transfer with exit 97
   (`CurlExitCode.Proxy`) `Send failure: Socket is not connected`, before the file name is
   checked, with no datagram channel and no proxy connection opened.
2. **HTTPS:** an HTTPS proxy is treated as rule 4 treats an HTTP one, over TLS: the proxy
   connection is `ConnectTarget(proxy, UseTls: true) { IsForwardProxy = true }`, as the HTTP
   handler reaches a forward proxy, and the request line names `https://`.
3. **The reply is read,** for HTTP and HTTPS proxies alike, up to the end of its header
   block: `101` or `2xx` is exit 7 `bind() failed; Invalid arguments`; any other status is
   exit 7 `CONNECT-UDP tunnel failed, response N`, `N` being 0 for a first line that is not
   an HTTP status line; a close before the block ends is exit 56 `Proxy CONNECT aborted`.
   This corrects BL-345's handler, which did not read the reply and so reported `bind()`
   for a refusing proxy too.
4. A TLS failure to the proxy is the connector's result, returned unchanged, as for any
   proxy the connector cannot reach.

## Consequences

- No proxy kind lets TFTP traffic bypass the proxy.
- A close over TLS without `close_notify` is reported as `Proxy CONNECT aborted` (exit 56),
  where curl reports Schannel's own exit 56 text `schannel: server closed abruptly (missing
  close_notify)`; the exit code matches, the TLS-layer text is the connector's concern.
- The reply's header block is not size-limited here, unlike the CONNECT reply
  (`HttpProxyTunnel`); an endless header from the proxy is read until the transfer is
  cancelled.
- The failure texts are Schannel-build measurements; a Linux or macOS build may differ.

## Alternatives considered

- **Tunnel TFTP through SOCKS5 UDP ASSOCIATE.** Lost: curl 8.21.0 does not; it fails with
  exit 97, and matching the reference build is the rule.
- **Keep not reading the reply.** Lost: measured curl reads it and reports a refusing
  proxy's status.
