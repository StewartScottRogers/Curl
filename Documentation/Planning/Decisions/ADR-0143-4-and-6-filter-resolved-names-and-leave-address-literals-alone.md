# ADR-0143 — `-4` and `-6` filter resolved names and leave address literals alone

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-500.

## Context

BL-499 parses `-4`/`--ipv4` and `-6`/`--ipv6` into `CommandLineOptions.IpAddressFamily`.
BL-500 makes the connectors honour it. Before pinning anything, curl 8.21.0 (Schannel,
Windows 11) was measured with `Record-CurlExchange.ps1` on 2026-09-28:

| Command | Result |
| --- | --- |
| `-4 -v http://127.0.0.1:P/`, `-6 -v http://127.0.0.1:P/` | Both `Trying 127.0.0.1:P...`, exit 0 |
| `-4 -v http://[::1]:P/`, `-6 -v http://[::1]:P/` | Both `Trying [::1]:P...`, exit 7 (no IPv6 listener) |
| `-4 -v http://localhost:P/` | `Host localhost:P was resolved.` / `IPv6: ::1` / `IPv4: 127.0.0.1` / `Trying 127.0.0.1:P...` only, exit 0 |
| `-6 -v http://localhost:P/` | The same three lines, then `Trying [::1]:P...` only, exit 7 |
| `-4 -v http://google.com:P/` | `IPv6: (none)` / `IPv4: <addresses>` |
| `-6 -v http://github.com:P/` (IPv4 only) | No resolved lines, `curl: (6) Could not resolve host: github.com` |
| `-6 -v --resolve foo:P:127.0.0.1 http://foo:P/` | `Added ... to DNS cache` / `Negative DNS entry` / ..., `curl: (6) Could not resolve host: foo` |
| `-4 -v --resolve foo:P:[::1] http://foo:P/` | The same, exit 6 |
| `-6 -v --resolve foo:P:127.0.0.1,[::1] http://foo:P/` | `Hostname foo was found in DNS cache`, both families reported, `Trying [::1]:P...` only |
| `-6 -v -x http://127.0.0.1:P http://example.com/` | Dials the proxy, exit 0 |
| `-6 -v -x http://bar:P --resolve bar:P:127.0.0.1 http://example.com/` | `curl: (5) Could not resolve proxy: bar` |
| `-6 -v --resolve foo:P:127.0.0.1 tftp://foo:P/x` | `curl: (6) Could not resolve host: foo` |
| `-6 -v tftp://127.0.0.1:P/x` | `Trying 127.0.0.1:P...` |

## Decision

1. The family is a constructor argument of `TcpConnector` and `UdpDatagramConnector`
   (`System.Net.Sockets.AddressFamily`: `InterNetwork`, `InterNetworkV6`, or `Unspecified` for
   either), mapped from the command line by `CurlComposition.AddressFamilyOf`. Both connectors
   are built per option group, as the option is, so no contract in
   `Curl.Protocol.Abstractions.UnitLibrary` changes.
2. A host or proxy that is an IP address literal is dialled as written, whatever the family, as
   measured. Every other name is dialled at the chosen family's addresses only
   (`AddressFamilyFilter`); one left with none fails as not resolved - exit 6 `Could not resolve
   host: <host>`, exit 5 `Could not resolve proxy: <host>` - with the messages curl uses when a
   name does not resolve at all.
3. A looked-up answer is cached and reported (`Host ... was resolved.`, `IPv6:`, `IPv4:`) with
   the chosen family's addresses only, as curl asks the system resolver for one family. The
   exceptions are `localhost` and names under `.localhost`, which curl answers itself with both
   loopback addresses: they are kept and reported whole and only dialled filtered.
4. A DNS cache entry (a `--resolve` entry, or `localhost`) is reported whole and dialled
   filtered; one left with no address of the family is reported as `Negative DNS entry`, in place
   of `Hostname ... was found in DNS cache` and the resolved lines, and fails the resolve.
5. SOCKS4 and SOCKS5, which resolve the target locally through the same cache, get the same
   filtering.

## Consequences

- `-4` and `-6` are honoured for every TCP scheme, through every proxy kind, and for TFTP.
- curl's further `-v` lines after a failed resolve (`Could not resolve: foo:P`) are not
  reported, as they are not for any exit 6 today.
