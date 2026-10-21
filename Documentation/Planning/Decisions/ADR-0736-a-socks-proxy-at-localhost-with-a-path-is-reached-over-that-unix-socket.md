# ADR-0736 — A SOCKS proxy at localhost with a path is reached over that Unix domain socket

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2955
- Decided by Claude under Stewart's delegation.

## Context

Since 7.84.0 curl reaches a SOCKS proxy over a Unix domain socket when the proxy text names the
host `localhost` and a path: `--socks5 localhost/path/to/sock`, `--proxy socks5h://localhost/path`.
Upstream's `parse_proxy` (lib/url.c, curl-8_22_0) does it for every SOCKS proxy type when the host
equals `UNIX_SOCKET_PREFIX` (`localhost`, compared without case) and the URL-decoded path is not
`/`; the SOCKS request still names the transfer's host (atyp 3 for socks5h) or its locally resolved
address. Upstream tests 1467, 1468 and 1470 run it. Curl read the path of proxy text and dropped it.

## Decision

- `ProxyUrlParser` reads the path, up to any query or fragment, and for a SOCKS kind at `localhost`
  with a path other than empty or `/` puts it, percent-decoded, on the new
  `ProxyEndpoint.UnixSocketPath`. Every other proxy keeps it `null`.
- `TcpConnector` dials that socket through `ITcpDialer.DialUnixSocketAsync` in place of resolving
  and dialling the proxy's host and port, writing `  Trying <path>:0...` as a `--unix-socket` dial
  does, then runs the SOCKS handshake as for a TCP hop. Its `Opened SOCKS connection` line names no
  local address on port 0 and the socket path on port 0 (`via <path> port 0`), which is what curl's
  `ipquad` holds for a Unix socket. A refused dial is the usual exit 7 proxy dial failure.
- No platform check: curl's Windows (Schannel) build also has the `UnixSockets` feature, so its
  `parse_proxy` reads the path there too. The upstream cases are `!win32` only because their test
  server's socket is a POSIX one.

## Consequences

The `-v` lines of a refused Unix-socket SOCKS dial and the exact `Opened SOCKS connection` text were
not measured against real curl (no Unix-socket SOCKS server on the lane's Windows machine); the
upstream cases 1467, 1468 and 1470, once the harness has a `socks5unix` server (BL-2859), check them.
