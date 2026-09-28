# ADR-0110 — FTP active mode looks up a `-P` name as a network interface off Windows

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-474.

## Context

ADR-0108 resolves a `-P` name through `IDnsResolver` and treats an interface name as a host
name, which is what the Windows (Schannel) build of curl 8.21.0 does: it has no
`getifaddrs`. The Linux and macOS OpenSSL builds ask `Curl_if2ip` first. In curl 8.21.0's
`lib/ftp.c`, `ftp_state_use_port` calls `Curl_if2ip(conn->remote_addr->family, scope, …)`
for any `-P` value but `-`:

- `IF2IP_FOUND`: the first address of the named interface (compared without regard to case)
  with the control connection's family, and for IPv6 the same `Curl_ipv6_scope` (unique
  local, link-local, site-local, node-local `::1`, or global), written by `inet_ntop` with no
  scope ID, is used as if it had been given as a literal.
- `IF2IP_AF_NOT_SUPPORTED`: the interface exists but has no such address, and the transfer
  ends with `CURLE_FTP_PORT_FAILED` (exit 30) without resolving the name.
- `IF2IP_NOT_FOUND`: the name is resolved as a host name (ADR-0108).

Measured with `Record-CurlExchange.ps1 -Ftp` on 2026-09-27 from WSL Ubuntu's curl 8.18.0
(OpenSSL 3.5.5; `-ListenAddress 172.26.96.1`, added for this, `-Curl wsl.exe`, `EPRT` and
`PORT` refused), `curl -v -P <value> ftp://172.26.96.1:47471/f.txt`, the machine's `lo`
holding `127.0.0.1`, `10.255.255.254` and `::1`, and `eth0` holding `172.26.99.197` and a
link-local IPv6 address:

- `-P lo`: `EPRT |1|127.0.0.1|37667|`, then `PORT 127,0,0,1,…`, exit 30 from the refusals.
- `-P LO`: `EPRT |1|127.0.0.1|58711|`: the name is compared without regard to case.
- `-P eth0`: `EPRT |1|172.26.99.197|38907|`.
- `-P nosuch.invalid`: `failed to resolve the address provided to PORT: nosuch.invalid` and
  exit 6. curl 8.18.0 names the URL's host in `Could not resolve host:`, where 8.21.0 names
  the `-P` value (ADR-0108); the 8.21.0 wording stands.

No 8.21.0 OpenSSL build was at hand; 8.18.0's results are taken as 8.21.0's, since they
match what 8.21.0's `ftp_state_use_port` above does.
`IF2IP_AF_NOT_SUPPORTED` was not measured: no interface on the WSL machine lacks
an IPv4 address, and the control connection cannot be IPv6 across WSL's NAT.

## Decision

- **A new seam, `INetworkInterfaceLookup`,** in `Curl.Protocol.Abstractions.UnitLibrary`:
  `FindAddresses(string interfaceName)` returns the named interface's unicast addresses of
  every family, or `null` when no interface has that name. The handler does the family and
  scope choice, so it is tested without a machine's interfaces.
- **`SystemNetworkInterfaceLookup`** in `Curl.Networking.UnitLibrary` implements it over
  `System.Net.NetworkInformation.NetworkInterface`, comparing names without regard to case,
  and finds nothing on Windows (`OperatingSystem.IsWindows()`), so every name there goes to
  the resolver, as the Schannel build's does. The platform choice lives in the lookup, not
  in the handler.
- **`FtpProtocolHandler` gains a constructor**
  `(IConnector, IConnectionListener, ITlsProvider, IDnsResolver, INetworkInterfaceLookup)`.
  The existing constructors look nothing up (`UnavailableNetworkInterfaceLookup`), so their
  behaviour is ADR-0108's.
- **Every `-P` value but `-` is looked up first**, a literal included, as curl does; no
  interface is named like a literal, so a literal still ends as the literal.
- **The family and scope are the control connection's own address's** (its local end,
  IPv4-mapped taken as IPv4), where curl takes its remote end's. Both ends of one connection
  share a family and, but for unusual routing, a scope, and the fake connections the tests
  use report only a local end. An unknown local address finds no interface address: exit 30
  after `QUIT`, as `-P -` ends then.
- **An interface with no address of that family and scope** ends with exit 30,
  `Failed to do PORT`, after `QUIT`, with no `-v` line, as `-P -` ends when the control
  connection's address is unknown.
- The found address is announced without its IPv6 scope ID, as `inet_ntop` writes it.
- Wiring the lookup into `Curl.Console` belongs with BL-458, which wires the listener, the
  TLS provider and the resolver into the same constructor call.

## Consequences

- One more contract in `Curl.Protocol.Abstractions.UnitLibrary`; no protocol but FTP uses it.
- Off Windows, `-P lo` and `-P eth0` announce the interface's address once the composition
  passes `SystemNetworkInterfaceLookup`; on Windows nothing changes.
- `NetworkInterface` lists interfaces and addresses in the system's order, which is
  `getifaddrs`'s order on Linux; a machine where they differ would pick a different first
  address.
- `Curl_if2ip`'s `-P` scope-ID check (`conn->scope_id`, set from a URL's `%zone`) is not
  modelled.

## Alternatives considered

- **Deciding the platform in the handler** with `OperatingSystem.IsWindows()`: a protocol
  library would then pin one platform's answer per test run, and the Windows lanes could
  never cover the other branch.
- **Returning only the chosen address from the lookup**: the scope rule would move into
  `Curl.Networking.UnitLibrary`, far from the FTP code that needs it, and `null` could not
  tell "no interface" (resolve the name) from "no address of that family" (exit 30).
- **Resolving the interface through `IDnsResolver`**: a resolver answers host names; an
  interface is a different question with a different failure (exit 30, not 6).
