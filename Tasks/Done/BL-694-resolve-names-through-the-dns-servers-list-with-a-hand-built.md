---
id: BL-694
title: Resolve names through the --dns-servers list with a hand-built DNS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-643, BL-640]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions/ADR-0170-dns-servers-resolve-through-a-hand-built-c-ares-style-client-inside-networking.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-694 — Resolve names through the --dns-servers list with a hand-built DNS client

## Goal

With `--dns-servers <list>`, names are resolved by a hand-built DNS client in `Curl.Networking.UnitLibrary` that sends A and AAAA queries over UDP (retrying over TCP on truncation) to those servers, binding its socket per `--dns-interface`, `--dns-ipv4-addr` and `--dns-ipv6-addr`, as curl's c-ares build does, on every platform.

## Context

- Conformance audit 2026-09-28, row 28; standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): the c-ares options are supported on every platform because curl's c-ares builds support them. Parsing: BL-643. Message codec: BL-640 (reuse it; do not write a second one).
- curl documents `--dns-servers` as "Set the list of DNS servers to be used instead of the system default" (https://curl.se/docs/manpage.html, checked 2026-09-28); the port syntax (`host:port`, `[v6]:port`) and the order servers are tried follow c-ares; record them from the manual and a measurement with a c-ares build of curl in Notes.
- The UDP socket sits behind a thin datagram seam like `ITcpDialer` (ADR-0083), so tests need no network; the resolver implements `IDnsResolver` and is composed in `Curl.Console/CurlTransports.cs` when the option is given. Failures map to exit 6 with curl's message.
- This client is also the SRV lookup BL-689 needs for Kerberos KDC location; expose SRV queries on it.

## Acceptance criteria

- [x] Measured first with a c-ares build of curl through `Record-CurlExchange.ps1` extended as needed (a loopback DNS responder): the query bytes, the server order, the truncation fallback, a dead server; copied into Notes.
- [x] `Curl.Networking.UnitTests` through the datagram seam pin the query bytes, server order and timeouts on a fake `TimeProvider`, the TCP retry on truncation, the bind for each of the three binding options, SRV answers, and exit 6 with the measured message.
- [x] A `Curl.Console.UnitTests` test shows `--dns-servers` replacing the system resolver for a transfer.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

### Measurements (2026-09-28)

c-ares build: Alpine `curl 8.22.0 ... OpenSSL/3.5.8 ... c-ares/1.34.8`, installed into an Alpine 3.24.2
minirootfs chroot at `/root/alp` in WSL (`/tmp` does not survive WSL's idle shutdown), run as
`Record-CurlExchange.ps1 -NoServer -ListenAddress 172.26.96.1 -DnsPort <p> -Curl wsl.exe -CurlArgs
'-u','root','-e','chroot','/root/alp','curl','-sS',...,'http://bl694.example:1/'` (172.26.96.1 is the Windows
host on the WSL network). `Record-CurlExchange.ps1` gained `-DnsPort`, `-DnsSilentPort`, `-DnsAnswerAddress`,
`-DnsTruncate` and `-DnsResponseCode`: a UDP+TCP responder writing `dns.txt` (ms, transport, port, query hex).

Query bytes (`--dns-servers H:15353`), AAAA then A at the same millisecond, random IDs, one shared cookie:

    udp 66040100000100000000000105626c363934076578616d706c6500001c000100002904d000000000000c000a0008355c114d1edb1fdf
    udp 9d890100000100000000000105626c363934076578616d706c65000001000100002904d000000000000c000a0008355c114d1edb1fdf

Truncation (`-DnsTruncate`): each UDP query followed at once (3 ms) by the same query over TCP, same ID,
OPT with no option: `832a...00001c000100002904d0000000000000`. Exit 7 from the port-1 connect, i.e. resolved.

| Case | Queries seen (ms, server) | Exit | stderr |
| --- | --- | ---: | --- |
| silent `:15360`, answering `:15361` | 118 x2 to 15360, 2118 x2 to 15361 | 7 | resolved, connect refused |
| one silent server | 87 x2, 2087 x2, 4639, 5893 | 6 | `Could not resolve host: bl694.example (Timeout while contacting DNS servers)` after 13.4 s |
| three silent servers, `-m 30` | 70/2071/4071 (one per server), 6072/8436-8963/11700-11750, 14851-27334 | 28 | `Resolving timed out after 30000 milliseconds` (3 rounds of the list) |
| NXDOMAIN | 2 | 6 | `... (Domain name not found)` |
| SERVFAIL | 6 (3 rounds x 2 types) | 6 | `... (DNS server returned general failure)` |
| `-4`, only AAAA data | 1 (A only) | 6 | `... (DNS server returned answer with no data)` |
| `-6` | 1 (AAAA only) | 7 | resolved |
| closed port then answering | 2097 x2 to the second | 7 | (WSL-to-Windows drops the ICMP, so a closed port looks silent) |
| `--dns-ipv4-addr 10.9.9.9` (not local) | 0 | 6 | `... (Could not contact DNS servers)` |
| `--dns-ipv4-addr 172.26.99.197` (local), `--dns-interface eth0`, `--dns-interface nosuchif0` | 2 | 7 | resolved |
| list `"H:p, H:p"`, `" H:p"`, `"H:p,"`, `"::1,H:p"`, `"[::1]:p,H:p"` | 2 | 7 | accepted |
| list `H:0` | 0 at p (sent to port 53) | 6 | `... (DNS server returned general failure)` |
| list `localhost:p`, `H:p;H:p` | 0 | 43 | `Error 43 resolving bl694.example:1` |

Our build against the same responder (`-Curl Curl.Console\bin\Debug\net10.0\curl.exe`, `--dns-servers 127.0.0.1:p`):
the same query layout (AAAA then A, flags 0100, OPT 1232 with an 8-byte cookie), the TCP retry on truncation,
`(6) Could not resolve host: bl694.example (Domain name not found)` for NXDOMAIN and `(43) Error 43 resolving
bl694.example:1` for `--dns-servers bogus`.

### Decisions (ADR-0170)

- The timing is c-ares' shape without its jitter: 3 rounds of the list, 2 s then 4 s then 8 s per attempt.
- `--dns-interface` binds the interface's first address of the server's family (a global IPv6 one before a
  link-local one); a missing interface leaves the socket on any address, as the c-ares build went on.
- Without `--dns-servers`, a binding option alone makes the client ask the system's servers
  (`SystemDnsServers`, leaving out Windows' dead `fec0::` defaults).
- The reason printed is the last query's (A under both families); AAAA addresses come before A ones.
- SRV support went into `DnsAnswerDecoder` (`DnsAnswer.ServiceRecords`), not a second codec, and
  `DnsServerResolver.ResolveServiceAsync` exposes it for BL-689's KDC location.
- Touches widened with `Record-CurlExchange.ps1` (the task asks to extend it), ADR-0170 and the ADR index;
  no task in Doing names any of them.

### Review

The code-reviewer found no must-fix bug. Applied: replies matched by address bytes and port, ignoring an
IPv6 scope ID; an answer holding only records of another type or class is NoData, not a retried BadReply;
the question's name is case-folded for ASCII letters only; link-local and `fec0::` handling above. Filed
BL-824 for the SOCKS local-resolve path, which still prints the plain exit 6 message.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr resolve through a hand-built c-ares-style DNS client (UDP, TCP on truncation, SRV), with c-ares' failure texts and exit 43
