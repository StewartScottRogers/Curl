---
id: BL-2955
title: Reach a SOCKS proxy over a Unix domain socket for socks5://localhost/path and socks5h://localhost/path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferProxySelection.cs, Curl.Engine.UnitTests/TransferProxySelectionTests.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/SocksProxyTunnel.cs, Curl.Networking.UnitTests/TcpConnectorTests.cs, Curl.Networking.UnitTests/SocksProxyTunnelTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2955 — Reach a SOCKS proxy over a Unix domain socket for socks5://localhost/path and socks5h://localhost/path

## Goal

Off Windows, `--socks5 localhost/path/to/sock` and `--proxy socks5h://localhost/path/to/sock` (also `socks4`, `socks4a`, `socks5` schemes) connect to the SOCKS proxy over the Unix domain socket at `/path/to/sock`, as curl does since 7.84.0, and run the SOCKS handshake over it.

## Context

Found by BL-2859, which needs it for upstream cases 1467, 1468 and 1470 (features `proxy,UnixSockets,!win32`, server `socks5unix`; commands `--socks5 localhost%SOCKSUNIXPATH http://%HOSTIP:%HTTPPORT/1467` and `https://this.is.a.host.name:%HTTPSPORT/1470 --insecure --proxy socks5h://localhost%SOCKSUNIXPATH`). Curl today has no proxy-over-Unix-socket path: `TransferProxySelection` and `TcpConnector`/`SocksProxyTunnel` dial the proxy's host and port only; `UnixSocketAddress` (Curl.Networking.UnitLibrary) already dials a Unix socket for `--unix-socket`, so reuse it. Upstream: lib/url.c `parse_proxy` (curl-8_22_0): for a SOCKS scheme whose host is `localhost` and whose URL has a path, the path names the Unix socket (`UNIX_SOCKET_PREFIX`), and the hostname sent in the SOCKS request is the transfer's host (atyp 3 for socks5h). Measure real curl on Linux behaviour from upstream's case files rather than Windows (curl rejects Windows paths there). If the change lands in other files than `touches` names, widen it per the lane rules.

## Acceptance criteria

- [ ] A unit test in `Curl.Engine.UnitTests` or `Curl.Networking.UnitTests` shows `socks5h://localhost/some/path` selects a SOCKS5 proxy reached over the Unix socket `/some/path`, sending the target host name (atyp 3) in the SOCKS request.
- [ ] A unit test shows `--socks5 localhost/some/path` does the same with SOCKS5 local resolution.
- [ ] A SOCKS proxy URL with a host other than `localhost`, or with no path, still dials host and port (existing tests stay green).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: e440f871 5c6bbdcf 9641394d dd1cc079.
