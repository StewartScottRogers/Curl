# ADR-0108 — FTP active mode resolves a `-P` name through the injected DNS resolver

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-466.

## Context

BL-437 gave `FtpProtocolHandler` active mode for `-P -` and IPv4 and IPv6 literals; any
other `-P` address ended with exit 6, `Could not resolve host: <name>`, and no `QUIT`,
because no DNS seam reached the handler (ADR-0102's BL-437 addendum).

curl 8.21.0's `lib/ftp.c`, `ftp_state_use_port`, first asks `Curl_if2ip` whether the
address is a network interface name, then resolves what is left as a host name and binds
and announces the first address it can open a socket for. A name that does not resolve is
`Could not resolve host: <name>`, then the info line
`failed to resolve the address provided to PORT: <name>`, and exit 6.

Measured with `Record-CurlExchange.ps1 -Ftp` on 2026-09-27 (curl 8.21.0, Schannel build),
`curl -v -P <value> ftp://127.0.0.1:47466/f.txt`:

- `-P localhost`: `EPRT |2|::1|58064|`, then the transfer, exit 0. The name resolved to
  `::1` first, and curl announced an IPv6 address on an IPv4 control connection.
- `-P nosuch.invalid`: after `PWD`, `* Could not resolve host: nosuch.invalid`,
  `* failed to resolve the address provided to PORT: nosuch.invalid`, no `QUIT`, exit 6,
  `curl: (6) Could not resolve host: nosuch.invalid`.
- `-P "Loopback Pseudo-Interface 1"` (the Windows loopback interface): the same two lines
  and exit 6. The Windows build has no `getifaddrs`, so `Curl_if2ip` finds no interface
  and the name goes to the resolver.

`Curl.Protocol.Abstractions.UnitLibrary` already has `IDnsResolver`, implemented by
`SystemDnsResolver` in `Curl.Networking.UnitLibrary` and used by `TcpConnector`.

## Decision

- **The handler takes an `IDnsResolver`.** `FtpProtocolHandler` gains a constructor
  `(IConnector, IConnectionListener, ITlsProvider, IDnsResolver)`. The handler resolves the
  name and passes an address to `IConnectionListener`; `ListenTarget` keeps holding one
  `IPAddress`. The existing constructors resolve nothing (`UnavailableDnsResolver`), so a
  handler built without a resolver ends every name with exit 6 as before.
- **The first resolved address is used**, IPv4-mapped announced as IPv4, as a literal is.
  The resolver's order is the system's; `SystemDnsResolver` returns `::1` first for
  `localhost` on Windows, which matches the measured `EPRT`.
- **A name that does not resolve** (the resolver returns no address) reports curl's two
  `-v` lines through `ITransferEvents.ReportInfo` and ends with exit 6,
  `Could not resolve host: <name>`, and no `QUIT`.
- **A literal is never sent to the resolver**, and `-P -` still uses the control
  connection's address.
- **Interface names are host names.** This matches the Windows (Schannel) build as
  measured. The Linux and macOS OpenSSL builds look an interface name up with `getifaddrs`
  first; that is not done here. (BL-474, ADR-0110: an injected `INetworkInterfaceLookup`
  now does it before the resolver, and finds nothing on Windows.)
- Wiring the resolver into `Curl.Console` is part of BL-458, which wires the listener and
  TLS provider into the same constructor call.

## Consequences

- No change to `Curl.Protocol.Abstractions.UnitLibrary`.
- Once the composition hands the handler the resolver `TcpConnector` uses, a `-P` name is
  resolved the way a URL host is.
- curl tries the next resolved address when `socket()` fails for one; the handler binds
  only the first. A failure to bind it is the listener's exit code, as for a literal.
- The exit 6 message is not truncated to curl's 256-byte error buffer here
  (`CurlErrorBuffer` lives in `Curl.Networking.UnitLibrary`, which a protocol library may
  not reference), so for a `-P` name of more than about 230 characters the message is
  longer than curl's.

## Alternatives considered

- **A host name on `ListenTarget`**, resolved by the listener: a change to the shared
  contract, and the listener would have to report curl's `-v` lines, which it has no
  `ITransferEvents` for.
- **A new resolver seam just for FTP**: `IDnsResolver` already says exactly this.
