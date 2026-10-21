---
id: BL-2955
title: Reach a SOCKS proxy over a Unix domain socket for socks5://localhost/path and socks5h://localhost/path
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferProxySelection.cs, Curl.Engine.UnitTests/TransferProxySelectionTests.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitLibrary/SocksProxyTunnel.cs, Curl.Networking.UnitTests/TcpConnectorTests.cs, Curl.Networking.UnitTests/SocksProxyTunnelTests.cs, Curl.Networking.UnitTests/TcpConnectorTests.SocksUnixSocket.cs, Curl.Core.UnitLibrary/ProxyUrlParser.cs, Curl.Core.UnitTests/ProxyUrlParserTests.cs, Curl.Protocol.Abstractions.UnitLibrary/ProxyEndpoint.cs, Curl.Protocol.Abstractions.UnitTests/ProxyEndpointTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2955 — Reach a SOCKS proxy over a Unix domain socket for socks5://localhost/path and socks5h://localhost/path

## Goal

Off Windows, `--socks5 localhost/path/to/sock` and `--proxy socks5h://localhost/path/to/sock` (also `socks4`, `socks4a`, `socks5` schemes) connect to the SOCKS proxy over the Unix domain socket at `/path/to/sock`, as curl does since 7.84.0, and run the SOCKS handshake over it.

## Context

Found by BL-2859, which needs it for upstream cases 1467, 1468 and 1470 (features `proxy,UnixSockets,!win32`, server `socks5unix`; commands `--socks5 localhost%SOCKSUNIXPATH http://%HOSTIP:%HTTPPORT/1467` and `https://this.is.a.host.name:%HTTPSPORT/1470 --insecure --proxy socks5h://localhost%SOCKSUNIXPATH`). Curl today has no proxy-over-Unix-socket path: `TransferProxySelection` and `TcpConnector`/`SocksProxyTunnel` dial the proxy's host and port only; `UnixSocketAddress` (Curl.Networking.UnitLibrary) already dials a Unix socket for `--unix-socket`, so reuse it. Upstream: lib/url.c `parse_proxy` (curl-8_22_0): for a SOCKS scheme whose host is `localhost` and whose URL has a path, the path names the Unix socket (`UNIX_SOCKET_PREFIX`), and the hostname sent in the SOCKS request is the transfer's host (atyp 3 for socks5h). Measure real curl on Linux behaviour from upstream's case files rather than Windows (curl rejects Windows paths there). If the change lands in other files than `touches` names, widen it per the lane rules.

## Acceptance criteria

- [x] A unit test in `Curl.Engine.UnitTests` or `Curl.Networking.UnitTests` shows `socks5h://localhost/some/path` selects a SOCKS5 proxy reached over the Unix socket `/some/path`, sending the target host name (atyp 3) in the SOCKS request.
- [x] A unit test shows `--socks5 localhost/some/path` does the same with SOCKS5 local resolution.
- [x] A SOCKS proxy URL with a host other than `localhost`, or with no path, still dials host and port (existing tests stay green).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Widened `touches` (2026-10-21, lane 1): the proxy text is parsed in `Curl.Core`'s `ProxyUrlParser` and the endpoint is `Curl.Protocol.Abstractions`' `ProxyEndpoint`, so the socket path is read there and carried as a new `ProxyEndpoint.UnixSocketPath`; `TcpConnectorTests.SocksUnixSocket.cs` is a new partial. No task in Doing on `origin/work/dark-factory` names any of them.
- Design (as upstream's `parse_proxy`, curl-8_22_0): for a SOCKS kind (scheme or the option's kind) whose host is `localhost` (any case) and whose URL path is neither empty nor `/`, the percent-decoded path is the Unix socket the proxy is reached over. `TransferProxySelection` needed no change: the path rides on the selected `ProxyEndpoint` (and survives `-U`'s `with`). `TcpConnector` dials that socket through `ITcpDialer.DialUnixSocketAsync` in place of resolving and dialling the proxy host, then runs the SOCKS handshake as for any SOCKS hop; the target host is still sent (atyp 3 for socks5h) or resolved locally (socks5).
- Choice: no platform check. curl's Windows build also has `UnixSockets`, so `parse_proxy` reads the path there too; the upstream cases are `!win32` only because the test server's socket is POSIX.
- Over a Unix socket `OpenSocksHopAsync` had no TCP end points for its `Opened SOCKS connection` line (NullReferenceException); `SocksOpenedLine` now names no local address on port 0 and `via <path> port 0`, as curl's `ipquad` holds for a Unix socket. Not measured against real curl (no Unix-socket SOCKS server here); recorded in ADR-0736.
- Tests: `TcpConnectorTests.SocksUnixSocket.cs` (socks5h sends `05 01 00 03 0E target.example 1F 90` over the dialled `/some/path`; socks5 sends the locally resolved 10.1.2.3; `Trying /some/path:0...`; refused dial exit 7), `TransferProxySelectionTests` (both proxy forms, `-U` keeps the socket, non-localhost or no path dials host and port), `ProxyUrlParserTests.TryParse_SocksAtLocalhostWithAPath_IsReachedOverThatUnixSocket`, `ProxyEndpointTests`.
- Verified 2026-10-21: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (43 projects, 0 failed); `Measure-CodeQuality.ps1` for Curl.Networking, Curl.Protocol.Abstractions and (after splitting `ReadParts`, complexity 12 -> `SplitScheme`/`PathOf`) Curl.Core: 100% line and branch, 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: e440f871 5c6bbdcf 9641394d dd1cc079.
- 2026-10-21: Doing -> Done. socks5h://localhost/path and --socks5 localhost/path reach the SOCKS proxy over that Unix domain socket
