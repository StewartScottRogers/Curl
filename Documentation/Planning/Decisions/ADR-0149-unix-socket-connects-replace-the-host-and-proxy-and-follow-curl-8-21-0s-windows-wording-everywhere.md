# ADR-0149 — Unix socket connects replace the host and proxy and follow curl 8.21.0's Windows wording everywhere

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-507.

## Context

BL-506 parses `--unix-socket <path>` and `--abstract-unix-socket <path>` into
`CommandLineOptions.UnixSocketPath` and `UnixSocketIsAbstract`. BL-507 makes the connector dial
that socket. `Record-CurlExchange.ps1` gained `-UnixSocket <path>` (a Unix domain socket listener
in place of TCP) for the measurements, which are copied in full in BL-507's Notes. In short:

| Case | curl 8.21.0, Windows (Schannel) | curl 8.18.0, Ubuntu (OpenSSL) |
| --- | --- | --- |
| Listening socket | `Trying <path45>:0...`, `Established connection to <path> (<path45> port 0) from  port 0 `, `Host:` the URL's | `Trying <path>:0...`, `Established connection to <URL host> (<path> port 0) from  port 0 ` |
| Missing socket | `Immediate connect fail for <path45>: Connection refused`, `connect to <path45> port 0 from  port 0 failed: ...`, exit 7 `Failed to connect to <host>:<port> over unix://<path> after N ms: Could not connect to server` | `Immediate connect fail ...: No such file or directory`, no `connect to` line, exit 7 `Failed to connect to <host> over <path> after N ms: ...` |
| Missing directory | `Network down` | `No such file or directory` |
| `--abstract-unix-socket x` | `Trying :0...`, `Invalid arguments`, exit 7 `... over unix://x ...` | Connects when something listens on `@x`; `Connection refused` when not |
| 108-byte path | exit 6 `Unix socket path too long: '<path>'`; 107 bytes dial | - |
| `-x` given as well | The proxy is dropped unread: `-x "http://[bad"` still dials the socket (exit 7, not exit 5) | A valid `-x` is ignored: origin-form `GET /` goes to the socket |

`<path45>` is the path cut to 45 characters: curl keeps the remote address in a `MAX_IPADR_LEN`
(46 byte) buffer. No curl 8.21.0 OpenSSL build was available on Linux or macOS.

## Decision

1. `TcpConnector` takes the socket as a constructor argument (`UnixSocketAddress`), built per
   option group by `CurlComposition.UnixSocketOf`, as `-4`/`-6` are (ADR-0143). With one set,
   every connect dials it through `ITcpDialer.DialUnixSocketAsync` in place of resolving and
   dialling the host, port or proxy: no `IDnsResolver` call, no `--resolve` or `--connect-to`
   lookup. TLS, when the target asks for it, runs to the URL's host over the socket. A
   `--resolve` or `--connect-to` entry that does not parse still fails with exit 49 first, as that
   check comes before any dial (not measured with a socket).
2. The `-v` lines and exit 7 message are curl 8.21.0's Windows ones on every platform: the Ubuntu
   measurement is of 8.18.0, whose wording 8.21.0 changed (the `unix://` form, the `:<port>`, the
   `connect to` line, the socket named as the host), and the text comes from curl's common code,
   not its TLS backend. Only the reason after `failed:` is the platform's
   (`ConnectFailureReason`, which gained `Invalid arguments` for WSAEINVAL).
3. The `Established connection` line names the socket path as the host and the cut path as the
   address. `ConnectionOpenedEvent` gains the optional `UnixSocketRemoteIp`, which
   `TransferEventInfoText` prints in place of the end points; its two `IPEndPoint`s stay required
   and are `0.0.0.0` port `0` for a socket. That is additive, so no other consumer changes.
4. The proxy is dropped unread under a socket (`TransferProxySelection`), as measured, so a bad
   `-x` or proxy variable cannot fail the transfer and the request is origin-form.
5. `sun_path` holds 108 bytes, 104 on macOS: a path whose UTF-8 bytes and one more do not fit is
   exit 6 `Unix socket path too long: '<path>'`, as curl's `Curl_unix2addr` refuses it. macOS's
   104 is from `sockaddr_un`, not measured.
6. An abstract name is dialled as a leading NUL on every platform, as curl does; Windows then
   fails with `Invalid arguments` (measured), Linux uses the abstract namespace.
7. Pools stay per option group (each group builds its own transports), so two sockets never share
   a pooled connection. When BL-754 shares one pool across groups, the socket must join
   `ConnectionPoolKey`.

## Consequences

- `--unix-socket` and `--abstract-unix-socket` work for every TCP scheme.
- Two measured differences are left to follow-up tasks outside this one's projects: the HTTP
  handler's `Connection #0 to host <path lower-cased>:0 left intact` line (it prints the URL's
  host and port), and `%{remote_ip}` (the cut path), `%{remote_port}` and `%{local_port}` (`-1`)
  for a socket connection.
