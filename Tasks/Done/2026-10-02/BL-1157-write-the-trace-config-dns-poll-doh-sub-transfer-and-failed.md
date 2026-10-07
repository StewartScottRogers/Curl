---
id: BL-1157
title: Write the --trace-config dns poll, DoH sub-transfer and failed-resolve lines
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1102]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1157 — Write the --trace-config dns poll, DoH sub-transfer and failed-resolve lines

## Goal

Under `-v --trace-config dns` (or `doh`, `all`) Curl also writes the `[DNS]` lines BL-1102 left out: a failed resolve's, the DoH sub-transfers' own `-v` lines prefixed `[DNS] `, and the connect lines through a proxy or Unix socket.

## Context

- ADR-0356 (BL-1102) writes the filter's lines around a direct connect (`DnsFilterTraceEvents`, `TcpConnector.TracesDnsFilter`) and routes `DohDnsResolver`'s lines through `FlowScopedTransferEvents`. Its decision 4 lists what is left; BL-1102's Notes hold curl 8.21.0's measured stderr.
- A failed resolve under DoH ends: `Could not resolve host: example.test`, `[DNS] cache negative name resolve for example.test:P type=A+AAAA`, `Could not resolve: example.test:P`, `[DNS] error resolving: 6`, `[DNS] Curl_conn_connect(block=0) -> 6, done=0`, `[DNS] Curl_conn_connect(), filter returned 6`, `[DNS] [1] shutdown async`, `closing connection #0`, `[DNS] [1] destroy async`.
- The DoH sub-transfers' lines (`[DNS] [DNS] created DNS filter for 127.0.0.1:P ...`, `[DNS]   Trying ...`, TLS lines, `> POST` head, `[DNS] a DoH request is completed, 1 to go`) come from the DoH connector's events, which today are none.
- The multi loop's `resolve incomplete ...` and repeated `done=0` lines depend on poll timing; ADR-0356 decided not to reproduce them. Measure again before changing that.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (`-Tls` for DoH): a failed DoH resolve, a successful one, and `-x http://127.0.0.1:P` under `--trace-config dns -v`; stderr in Notes.
- [x] `Curl.Console.UnitTests` pin the failed-resolve lines as measured, except the poll-timing lines (`CurlCommandRunnerFailedResolveTraceTests`). The DoH sub-transfer lines were split out to BL-1180 (see Notes).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

Measured 2026-10-02, curl 8.21.0 (Schannel), `Record-CurlExchange.ps1` (poll-timed `resolve incomplete`
and repeated `done=0` lines left out below):

- `-s -v --trace-config dns http://nonexistent.invalid:47114/` (exit 6): filter creation lines,
  `[DNS] queueing query [0/1] AAAA ...`, `... A ...`, `[DNS] Host nonexistent.invalid:47114 resolved IPv4: (none)`,
  `... IPv6: (none)`, `Could not resolve host: nonexistent.invalid` (twice),
  `[DNS] cache negative name resolve for nonexistent.invalid:47114 type=A+AAAA`,
  `Could not resolve: nonexistent.invalid:47114`, `[DNS] error resolving: 6`,
  `[DNS] Curl_conn_connect(block=0) -> 6, done=0`, `[DNS] Curl_conn_connect(), filter returned 6`,
  `[DNS] [1] shutdown async`, `closing connection #0`, `[DNS] [1] destroy async`. Plain `-v`: the two
  `Could not resolve host`, `Could not resolve: H:P`, `closing connection #0`. Under `-4`: `queries=1`, `type=A`.
- `-6 --resolve foo:47500:127.0.0.1 http://foo:47500/` (negative cache entry): `queries=2`,
  `[DNS] cache entry does not have type=AAAA addresses`, `Negative DNS entry`, `Could not resolve host: foo`,
  `Could not resolve: foo:47500`, `Could not resolve: foo`, the exit 6 lines, no async lines (BL-1181).
- Failed DoH (`-Tls -Connections 2`, 3-byte answers, `--doh-url https://127.0.0.1:47112/dns-query http://example.test:47113/`):
  as BL-1102 Notes; `Could not resolve host` once. Each sub-transfer: `[DNS] created DNS filter for 127.0.0.1:47112, transport=3, queries=3`,
  `[DNS] [DNS] added`, `[DNS] [DNS] cf_dns_start host 127.0.0.1:47112`, `[DNS]   Trying 127.0.0.1:47112...`,
  `[DNS] [DNS] Curl_conn_connect(block=0) -> 0, done=0`, (second only: `[DNS] Connection #1 is not open enough, cannot reuse`,
  `[DNS] Hostname 127.0.0.1 was found in DNS cache`), `[DNS] schannel: disabled automatic use of client certificate`,
  `[DNS] schannel: using IP address, SNI is not supported by OS.`, `[DNS] ALPN: curl offers http/1.1`,
  `[DNS] ALPN: server did not agree on a protocol. Uses default.`, `[DNS] [DNS] connected filter chain below`,
  `[DNS] [DNS] Curl_conn_connect(block=0) -> 0, done=1`, `[DNS] Established connection to 127.0.0.1 (127.0.0.1 port 47112) from 127.0.0.1 port N `,
  `[DNS] [DNS] removing connected setup filter`, `[DNS] [DNS] destroy`, `[DNS] using HTTP/1.x`, `> POST /dns-query HTTP/1.1` head,
  `} [30 bytes data]`, `[DNS] upload completely sent off: 30 bytes`, `< HTTP/1.1 200 OK` head, `{ [3 bytes data]`,
  `[DNS] Connection #1 to host 127.0.0.1:47112 left intact`, `[DNS] a DoH request is completed, 1 to go` (then `#2`, `0 to go`);
  the two sub-transfers' lines interleave.
- Successful DoH (a 46-byte A answer for example.test, refused target): the same sub-transfer lines, then
  `[DNS] DoH: Unexpected TYPE type AAAA for example.test`, `[DNS] hostname: example.test`, `[DoH] TTL: 60 seconds`,
  `[DoH] A: 127.0.0.1`, `[DNS] resolve complete for example.test:47113`, `Host example.test:47113 was resolved.`, ...,
  `Failed to connect to ...`, the exit 7 lines, `[DNS] [1] shutdown async`, `closing connection #0`, `[DNS] [1] destroy async`.
- `-x http://127.0.0.1:47115 http://example.test/`: the direct connect's filter lines, for the proxy's `127.0.0.1:47115`.

Plan and decisions (ADR-0366):

- `TcpConnector` writes `Could not resolve host:` (twice, once under DoH) and `Could not resolve: H:P` for a looked-up
  name with no address, plain `-v` included; `DnsFilterTraceEvents` brackets the last with the trace lines and takes the
  `-4`/`-6` family for `queries=`/`type=`; `AsyncResolveTeardownTraceEvents` (wrapped per transfer by `CurlCommandRunner`)
  writes `[DNS] [1] destroy async` after `closing connection #N`.
- Split: the DoH sub-transfer lines need the `[DNS] ` prefix applied to structured events, which `Curl.Output`'s
  writers word per TLS backend - a change in another library and a design of its own - so they went to BL-1180,
  with the acceptance criterion this task first carried for them. Proxy, Unix socket, negative cache entry and the
  async lines after a resolved name went to BL-1181.
- Tests: `DnsFilterTraceEventsTests`, `AsyncResolveTeardownTraceEventsTests`, `TcpConnectorTests.DnsFilterTrace`
  (three new), two updated `TcpConnectorTests` (`AddressFamily`, `DnsCache`) for the new plain `-v` lines,
  `CurlCommandRunnerFailedResolveTraceTests`. Coverage: `Curl.Networking.UnitLibrary` and `Curl.Console` 100% line and branch.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v writes curl's Could not resolve lines for a name that does not resolve, and --trace-config dns the filter's exit 6, negative-cache and async teardown lines; DoH sub-transfer lines split to BL-1180
