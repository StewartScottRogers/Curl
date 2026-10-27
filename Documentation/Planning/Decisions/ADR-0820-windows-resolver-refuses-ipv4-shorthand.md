# ADR-0820: The Windows build resolves no IPv4 address written in shorthand

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3575, CGR-046)

## Context

`IPAddress.TryParse` and `Dns.GetHostAddressesAsync` read every `inet_aton` form of an IPv4 address: fewer
than four parts (`127.1`, `0`, `2130706433`), hexadecimal or octal parts (`0x7f.0.0.1`) and leading zeros
(`127.0.0.01`). So `--connect-to foo.test:20500:127.1:20500` and `--interface 127.1` worked in Curl.
Measured on 2026-10-26 with `Record-CurlExchange.ps1` against a loopback server on 127.0.0.1:20500:

| Command | curl 8.21.0 Schannel (Windows) | curl 8.18.0 OpenSSL (Linux, WSL) |
| --- | --- | --- |
| `--interface 0`, `127.1`, `2130706433`, `127.0.0.01`, `host!127.1` | exit 45 `Failed binding local connection end` | `127.1` binds (exit 7 to a closed port, as without it) |
| `--connect-to foo.test:20500:<H>:20500` for `127.1`, `2130706433`, `127.0.0.01`, `0x7f.0.0.1`, `127.0.1` | exit 6 `Could not resolve host: <H>` | `127.1` dials 127.0.0.1 |
| `--interface 127.0.0.1`, `--connect-to ...:127.0.0.1:...` | exit 0 | exit 0 |
| `-x http://127.1:20500`, `http://127.1:20500/` | exit 0: the URL parser writes the host as `127.0.0.1` | exit 0 |
| `--resolve foo.test:20500:127.1` | exit 49 (already matched) | - |

## Decision

1. On Windows `SystemDnsResolver` resolves an IPv4 address in shorthand (`Ipv4ShorthandAddress.IsShorthand`:
   it parses as IPv4 but is not the dotted quad `IPAddress.ToString` writes) to nothing, so the connect is
   exit 6 and an `--interface` bind exit 45, as the Schannel build. Elsewhere it is looked up as before,
   as the OpenSSL build's `getaddrinfo` reads it. The platform is an internal constructor argument, so
   tests pin both answers on any platform.
2. `LocalBindingAddressChooser` binds only a dotted quad (or an IPv6 address) without the resolver; a
   shorthand goes to the resolver, whose answer decides.
3. `ProxyUrlParser` writes an IPv4 proxy host in shorthand as a dotted quad, as curl parses a proxy as a
   URL; otherwise `-x http://127.1:8080` would stop resolving on Windows. A transfer URL's host was already
   normalised by `CurlUrlHost`.

## Consequences

Scripts that pass shorthand addresses to `--connect-to` or `--interface` fail on Windows as with the
reference build. Other resolvers (`--doh-url`, `--dns-servers`) are unchanged.
