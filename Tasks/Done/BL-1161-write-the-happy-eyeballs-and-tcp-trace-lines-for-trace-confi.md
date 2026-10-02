---
id: BL-1161
title: Write the [HAPPY-EYEBALLS] and [TCP] trace lines for --trace-config happy-eyeballs, tcp, network, all and -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1103]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1161 — Write the [HAPPY-EYEBALLS] and [TCP] trace lines for --trace-config happy-eyeballs, tcp, network, all and -vvvv

## Goal

Curl writes curl 8.21.0's `[HAPPY-EYEBALLS]` and `[TCP]` lines under `--trace-config happy-eyeballs`, `tcp`, `network`, `all` and `-vvvv`.

## Context

- Split from BL-1103 (ADR-0357), which delivered `[SETUP]` and put `-vv`..`-vvvv`'s names into `CommandLineOptions.TraceComponents` (`-vvvv` adds `all`). BL-1103's Notes hold curl's measured stderr for `happy-eyeballs`, `tcp`, `network` and `-vvvv`, including the localhost two-family race and a refused connect.
- Follow the `SetupFilterTraceEvents`/`DnsFilterTraceEvents` pattern in `Curl.Networking.UnitLibrary` (layered over each other in `TcpConnector.TracingConnectionFilters`); `[TCP]` lines name socket descriptors (`fd=440`) and `send`/`recv` sizes, `[HAPPY-EYEBALLS]` repeats `checked connect attempts`/`adjust_pollset` once per poll.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` for each component named in the title (a plain HTTP transfer, a refused connect, and `localhost` where both families answer); stderr in Notes.
- [x] Tests pin each component's stable lines for a plain HTTP transfer, and that no line appears without its component; an ADR-0357 amendment records how the volatile values (fd numbers, nanosecond stamps, poll repetitions) are produced.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` (`200`, `Content-Length: 2`, `hi`), `-s -v --trace-config <c>`; fixtures in `%TEMP%\bl1161\<case>`. `-v` lines omitted.
  - `tcp`, `http://127.0.0.1:P/`: after `Trying`: `[TCP] Set TCP_KEEP* on fd=432`, `[TCP] cf_socket_open() -> 0, fd=432`, `[TCP] local address 127.0.0.1 port 54712...`, `[TCP] adjust_pollset, !connected, POLLOUT fd=432`, `[TCP] connected on fd=432`; then `Established`, `[TCP] query ALPN`, `using HTTP/1.x`, `[TCP] send(len=79) -> 0, 79`, the request, `[TCP] recv(len=102400) -> 81, 0`, `[TCP] recv(len=102400) -> 0, 40`, the response.
  - `happy-eyeballs`: `init ip ballers for transport 3`, `want to do more`, `check for next AAAA address: none`, `check for next A address: found`, `starting first attempt for ipv4 -> 0`, `Trying`, `checked connect attempts: 1 ongoing, 0 inconclusive`, `adjust_pollset -> 0, 1 socks`, `connect attempt #0 successful`, `Connected to 127.0.0.1 (127.0.0.1) port P`, `Established`, `removing connected setup filter`, `destroy`.
  - `network`: `[DNS]`, `[HAPPY-EYEBALLS]`, `[TCP]`, `[MULTI]`, `[TIMER]` interleaved: `... starting first attempt`, `Trying`, `[TCP]` open lines, `[HE] checked ...`, `[DNS] Curl_conn_connect(block=0) -> 0, done=0`, `[TCP] adjust_pollset`, `[HE] adjust_pollset`, (`[MULTI]` poll lines), `[TCP] connected`, `[HE] connect attempt #0 successful`, `[TIMER] [HAPPY_EYEBALLS] cleared`, `[HE] Connected to`, `[DNS] connected filter chain below`, `done=1`, `Established`, `[DNS] removing`/`destroy`, `[HE] removing`/`destroy`.
  - `happy-eyeballs,tcp,setup`, refused `http://127.0.0.1:1/` (exit 7, ~2 s): as above to `[HE] adjust_pollset -> 0, 1 socks`, then `[TCP] not connected yet on fd=400` / `checked` / `adjust_pollset` x2 more rounds, `[TCP] poll/select error on fd=400`, `connect to 127.0.0.1 port 1 from 0.0.0.0 port 54715 failed: Connection refused`, `[TCP] destroy`, `checked connect attempts: 0 ongoing, 0 inconclusive`, `want to do more`, `check for next AAAA address: none`, `check for next A address: none`, `no more attempts to try`, `baller 0: result=7`, `Failed to connect ...`. `local address 0.0.0.0 port 54715` here.
  - Same, `http://localhost:P/` (IPv4 listening): `check for next AAAA address: found`, `starting first attempt for ipv6 -> 0`, `Trying [::1]`, TCP open (fd=432, `local address :: port ...`), `checked: 1 ongoing`, `next HAPPY_EYEBALLS timeout in 200ms`, adjust round, `not connected yet on fd=432`, `checked`, `happy eyeballs timeout expired, start next attempt`, `want to do more`, `check for next A address: found`, `starting next attempt for ipv4 -> 0`, `not connected yet on fd=432`, `Trying 127.0.0.1`, TCP open (fd=452), `checked: 2 ongoing`, adjust 432, adjust 452, `adjust_pollset -> 0, 2 socks`, `not connected yet on fd=432`, `connected on fd=452`, `connect attempt #1 successful`, `[TCP] destroy`, `[TCP] cf_socket_close, fd=432`, `Connected to localhost (127.0.0.1) port P`, `Established`, `[SETUP] removing`/`destroy`, `[HE] removing`/`destroy`.
  - `dns,setup,happy-eyeballs,tcp,haproxy --haproxy-protocol`: removal order `[DNS]`, `[SETUP]`, `[HAPROXY]`, `[HAPPY-EYEBALLS]`; `[SETUP] added HAPROXY filter` then `[TCP] send(len=44) -> 0, 44` before `[DNS] connected filter chain below`.
- Split (as BL-1103 did): the `[TCP]` I/O lines (`query ALPN`, `send`, `recv`) come from the transfer, not the connect, and need a wrapping connection and the HTTP handler; filed as BL-1194. Recorded in the ADR-0357 amendment.
- Decisions (ADR-0357 amendment): fd numbered from 3 per connect; `local address` written as the family's unspecified address and port 0 (the dialler binds as it connects); one poll round per race wake-up rather than curl's per-poll repetitions; only a direct connect traced, as for `[DNS]`; `Set TCP_KEEP*` always written.
- Delivered: `ConnectAttemptTraceEvents` (Networking) under `TcpConnector.TracesHappyEyeballsFilter`/`TracesTcpFilter`, layered below `DnsFilterTraceEvents` and told the race's events by `AddressFamilyRace`; `TcpConnector` writes its removal after `[HAPROXY]`'s. `CurlComposition.TracesHappyEyeballs`/`TracesTcp` set them from `happy-eyeballs`/`tcp`/`network`/`all`. Curl's own output against a live server (Debug `curl.exe` through `Record-CurlExchange.ps1 -Curl`) matched the measured lines bar the volatile values.
- Tests: `ConnectAttemptTraceEventsTests` (plain transfer, localhost race, refused, each component alone, pass-through), `TcpConnectorTests.ConnectAttemptTrace` (all four filters in curl's order, refused, two-family race, tcp only), `CurlCompositionDnsTraceTests` (component rows, `-vvvv`, `network`); the existing `-vvvv`/`all`/`network` tests now set the new lines aside. `Measure-CodeQuality.ps1`: Curl.Networking and Curl.Console at 100% line and branch, no failing member.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config happy-eyeballs, tcp, network, all and -vvvv write curl's [HAPPY-EYEBALLS] and connect-attempt [TCP] lines
