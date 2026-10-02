---
id: BL-1102
title: Write the --trace-config dns and doh lines from the resolver and DoH resolver
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-649]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1102 — Write the --trace-config dns and doh lines from the resolver and DoH resolver

## Goal

Under `-v --trace-config dns`, `doh` or `all`, Curl writes the `[DNS]` lines curl 8.21.0 writes for its DNS connection filter, and the DoH resolver's `DoH request`, `DoH: <failure> type`, `hostname`, `[DoH] TTL/A/AAAA` lines reach the transfer's verbose output.

## Context

- ADR-0318 (BL-649) maps `dns` and `doh` here. `CommandLineOptions.TraceComponents` (BL-649) holds the names turned on, `all` standing for every one.
- `DohDnsResolver`'s four-argument constructor takes the sink and a `Func<CurlExitCode, string>` (BL-850); `CurlComposition.CreateDnsResolver` builds the resolver once per run, before any transfer's events exist, so the sink must be late-bound to the transfer that resolves (serial and `-Z`).
- Measured by BL-649 for a plain HTTP transfer to `127.0.0.1:P` under `--trace-config dns -v`: `[DNS] created DNS filter for 127.0.0.1:P, transport=3, queries=3`, `[DNS] added`, `[DNS] cf_dns_start host 127.0.0.1:P` before `Trying`, then `[DNS] Curl_conn_connect(block=0) -> 0, done=0`, `[DNS] connected filter chain below`, `[DNS] Curl_conn_connect(block=0) -> 0, done=1` before `Established connection`, then `[DNS] removing connected setup filter`, `[DNS] destroy`. BL-850's Notes: `doh` also turns on the whole `[DNS]` set; measure whether `dns` turns on the DoH lines.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (and `-Tls` for DoH): `--trace-config dns -v`, `--trace-config doh -v --doh-url ...`, `--trace-config dns -v --doh-url ...`; stderr in Notes.
- [x] `Curl.Console.UnitTests` pin the `[DNS]` lines for a plain transfer and the DoH lines under `doh`, and no such line without the component.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

Measured 2026-10-02, curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1`:

- `-s -v --trace-config dns http://127.0.0.1:47110/`:
  `* [DNS] created DNS filter for 127.0.0.1:47110, transport=3, queries=3`, `* [DNS] added`,
  `* [DNS] cf_dns_start host 127.0.0.1:47110`, `*   Trying 127.0.0.1:47110...`,
  `* [DNS] Curl_conn_connect(block=0) -> 0, done=0`, `* [DNS] connected filter chain below`,
  `* [DNS] Curl_conn_connect(block=0) -> 0, done=1`, `* Established connection to 127.0.0.1 ...`,
  `* [DNS] removing connected setup filter`, `* [DNS] destroy`, then the usual `-v` lines.
- `localhost`: the same, with `Host localhost:P was resolved.`/`IPv6`/`IPv4` after `cf_dns_start`
  and a `done=0` line after each of the two `Trying` lines.
- Refused (`127.0.0.1:47199`): ... `Trying`, three `done=0` lines (poll timing), `connect to ... failed`,
  `Failed to connect to ...`, `* [DNS] Curl_conn_connect(block=0) -> 7, done=0`,
  `* [DNS] Curl_conn_connect(), filter returned 7`, `* closing connection #0`.
- Without `-v`: nothing on stderr. `all` writes the whole engine with timestamps (ADR-0318's business).
- `-s -v --trace-config doh --doh-insecure --doh-url https://127.0.0.1:47112/dns-query http://example.test:47113/`
  (3-byte body answers) and the same with `dns`: byte-identical stderr, so `dns` turns the DoH lines
  on and `doh` the filter's. It holds the filter lines for example.test, `[DNS] resolve incomplete,
  queries=A+AAAA, responses=-, ongoing=0 for example.test:47113` and `done=0` lines on every poll, both
  DoH sub-transfers' `-v` lines prefixed `[DNS] ` (`[DNS] [DNS] created DNS filter for 127.0.0.1:47112`,
  `[DNS]   Trying ...`, schannel lines, `> POST` head, `[DNS] a DoH request is completed, 1 to go`),
  then `* [DNS] DoH: Too small type A for example.test`, `* [DNS] DoH: Too small type AAAA for example.test`,
  `* Could not resolve host: example.test`, `* [DNS] cache negative name resolve for example.test:47113 type=A+AAAA`,
  `* Could not resolve: example.test:47113`, `* [DNS] error resolving: 6`,
  `* [DNS] Curl_conn_connect(block=0) -> 6, done=0`, `* [DNS] Curl_conn_connect(), filter returned 6`,
  `* [DNS] [1] shutdown async`, `* closing connection #0`, `* [DNS] [1] destroy async`; exit 6.

Plan and decisions (ADR-0356):

- `CurlComposition.TracesDns`: `dns`, `doh` or `all` in `TraceComponents`, one switch as measured.
- `Curl.Networking`: `DnsFilterTraceEvents` decorates a direct connect's events (`TcpConnector.TracesDnsFilter`)
  and writes the filter lines around `Trying`, `ReportConnectionOpened` and `Failed to connect to`;
  one `done=0` per `Trying`, the extra poll lines left out as timing artefacts.
- `FlowScopedTransferEvents` (an `AsyncLocal` view) is the DoH resolver's sink; `TcpConnector.ResolverEvents`
  points it at the target's events before each look-up, so serial and `-Z` transfers each get their own lines.
- Filed BL-1157 for the rest: failed-resolve lines, DoH sub-transfer `[DNS]`-prefixed lines, proxy and Unix
  socket connects.
- Tests: `DnsFilterTraceEventsTests`, `FlowScopedTransferEventsTests`, `TcpConnectorTests.DnsFilterTrace`,
  `CurlCompositionDnsTraceTests` (plain transfer, with and without the component),
  `CurlCompositionDohTests.Connect_UnderTraceConfigDnsOrDoh_*` and `Connect_UnderAnotherTraceComponent_*`.
  Coverage: `Curl.Networking.UnitLibrary` and `Curl.Console` 100% line and branch.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --trace-config dns/doh/all writes curl's [DNS] filter lines around a direct connect and the DoH resolver's lines reach the resolving transfer
