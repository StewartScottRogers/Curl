# ADR-0170 — `--dns-servers` resolves through a hand-built c-ares-style client inside Networking

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-694.

## Context

`--dns-servers`, `--dns-interface`, `--dns-ipv4-addr` and `--dns-ipv6-addr` exist only in curl's
c-ares builds; the standing rule (root `CLAUDE.md`, 2026-09-28) is that Curl supports them on
every platform, with a DNS client written by hand. BL-643 parses them; BL-640 wrote the DoH
message codec (`DnsQueryEncoder`, `DnsAnswerDecoder`), which this client must reuse.

curl 8.22.0 with c-ares 1.34.8 (Alpine edge, in a WSL chroot; no 8.21.0 c-ares build was at
hand) was measured on 2026-09-28 through `Record-CurlExchange.ps1 -DnsPort`, a loopback DNS
responder added for this (UDP and TCP, silent ports, `-DnsTruncate`, `-DnsResponseCode`). The
measurements are in BL-694's Notes. In short:

- Each lookup sends an AAAA and an A query at once (only A under `-4`, only AAAA under `-6`), each
  with a random ID, flags `0x0100`, and one EDNS(0) OPT record advertising 1232 bytes that carries
  an 8-byte client cookie over UDP and no option over TCP.
- A truncated UDP reply is asked again over TCP at once, with the same ID.
- Servers are tried in list order; a silent one is given 2 s before the next is asked. With three
  silent servers c-ares went round the list three times, each round waiting longer, with jitter.
- NXDOMAIN ends the lookup (`Domain name not found`); SERVFAIL is retried on every round and ends
  as `DNS server returned general failure`; NOERROR without the type is `DNS server returned
  answer with no data`; silence is `Timeout while contacting DNS servers`; an unusable local
  address is `Could not contact DNS servers`. Each is exit 6 `Could not resolve host: <host>
  (<text>)`.
- A host name, a port over 65535 or a `;` in the list, or an address of the wrong family in
  `--dns-ipv4-addr`/`--dns-ipv6-addr`, is exit 43 `Error 43 resolving <host>:<port>`. Spaces around
  entries and empty entries are ignored; port 0 means 53; a bare IPv6 address is accepted.
- `--dns-interface nosuchif0` still resolves: c-ares goes on when it cannot bind to the device.

## Decision

1. **Where it lives.** `DnsServerResolver` in `Curl.Networking.UnitLibrary`, beside
   `SystemDnsResolver`, implementing `IDnsResolver` and a new `IDnsResolverWithFailureReason`, so
   `TcpConnector` and `UdpDatagramConnector` can print c-ares' reason (`NameResolutionFailure`) and
   exit 43. `CurlComposition.CreateDnsResolver` picks it when any of the four options is given; with
   only a binding option it asks the system's servers (`SystemDnsServers`, from
   `NetworkInterface.GetIPProperties().DnsAddresses`), as c-ares reads the system configuration.
2. **The codec is reused.** `DnsServerQuery` wraps `DnsQueryEncoder`'s header and question with the
   ID and OPT record, and reads replies through `DnsAnswerDecoder` after zeroing the matched ID (the
   decoder insists on DoH's ID 0). SRV support was added to that decoder (`DnsRecordType.Srv`,
   `DnsAnswer.ServiceRecords`) rather than written again, and `ResolveServiceAsync` exposes it for
   Kerberos KDC location (BL-689).
3. **The seam.** `IDnsSocketOpener` opens a bound UDP `IDatagramChannel` (the existing
   `UdpDatagramChannel`, given a local address) and a bound, connected TCP stream. Its production
   `DnsSocketOpener` is an ADR-0083 adapter; everything else is tested through a fake on a fake
   `TimeProvider`.
4. **Timing is deterministic.** Each query makes `Rounds` (3) passes over the list; an attempt
   waits 2 s in the first round and doubles each round (2 s, 4 s, 8 s), without c-ares' jitter,
   which cannot be pinned and changes nothing a script can rely on. One silent server fails after
   14 s where c-ares took 13.4 s.
5. **Which outcome ends a query.** An answer, NOERROR with no data and NXDOMAIN end it; any other
   response code, a timeout, an unreachable server and an unreadable reply move it to the next
   server. The lookup's reason is the last query's; the AAAA and A answers are returned in that
   order.
6. **Binding.** `--dns-ipv4-addr` binds sockets to IPv4 servers and `--dns-ipv6-addr` to IPv6 ones;
   otherwise `--dns-interface` binds the interface's first address of the server's family; an
   interface that does not exist, or has none, leaves the socket on any address. Binding to the
   interface's address rather than `SO_BINDTODEVICE` works on every platform without privileges,
   and the measured c-ares build ignored a failed device bind anyway.
7. **Answered without a query.** An IP address literal is returned as is, and `localhost` (and
   names under `.localhost`) as `::1` and `127.0.0.1`, as curl answers them itself.

## Consequences

- A c-ares-only option behaves as the c-ares build does on Windows, Linux and macOS alike.
- The reply to a query is matched by source end point, ID and question (name compared ignoring
  ASCII case); anything else on the socket is ignored.
- DNS cookies are sent but a server cookie is not remembered or echoed; FORMERR is not retried
  without EDNS. Neither was observable against the measured build.
