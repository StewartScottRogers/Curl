# ADR-0778 — IPv6 sockets are dual-stack, so IPv4-mapped literals connect

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3174
- Decided by Claude under Stewart's delegation.

## Context

`curl http://[::ffff:127.0.0.1]:<port>/` against a server listening on IPv4 only exited 7 in
Curl. `TcpDialer` opened an `AddressFamily.InterNetworkV6` socket with .NET's default
`DualMode = false` (`IPV6_V6ONLY` on), and Windows refuses an IPv4-mapped destination on such a
socket with `WSAEADDRNOTAVAIL` (reproduced 2026-10-22: `AddressNotAvailable`; the same socket
with `DualMode = true` connects from `[::ffff:127.0.0.1]`).

curl 8.22.0 (Windows, measured with `Record-CurlExchange.ps1`) connects and exits 0:
`Established connection to [::ffff:127.0.0.1] (::ffff:127.0.0.1 port N) from ::ffff:127.0.0.1
port M`, `Host: [::ffff:127.0.0.1]:N`. Its local end is the mapped address, so it dials an IPv6
socket with `IPV6_V6ONLY` off, not the IPv4 address the literal maps to. Linux and macOS default
to `IPV6_V6ONLY` off as well (Linux through `net.ipv6.bindv6only`).

## Decision

`TcpDialer.ApplySocketOptions` sets `Socket.DualMode = true` on every IPv6 socket, before any
local bind. `TcpConnector` dials the literal unchanged. Connecting through `IPAddress.MapToIPv4`
instead was rejected: it would change the local and remote end points curl reports (`-v`,
`%{local_ip}`, `%{remote_ip}`) from the mapped form curl prints.

## Consequences

- A mapped literal reaches an IPv4 server on every platform, whatever a Linux host's
  `bindv6only` says, with curl's end points.
- `--interface` and `--local-port` binds on an IPv6 socket bind a dual-stack socket, as curl's
  do on Linux and Windows.
- Tests: `Curl.Networking.UnitTests/TcpDialerMappedIPv4Tests.cs` (the option, a loopback connect
  without bytes, the connector's dial target) and
  `Curl.Console.IntegrationTests/CurlMappedIPv4LiteralIntegrationTests.cs` (the whole command).
