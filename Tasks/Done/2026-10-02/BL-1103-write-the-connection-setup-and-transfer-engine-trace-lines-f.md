---
id: BL-1103
title: Write the connection-setup and transfer-engine trace lines for --trace-config and -vv to -vvvv
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-649]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1103 — Write the connection-setup and transfer-engine trace lines for --trace-config and -vv to -vvvv

## Goal

Curl writes curl 8.21.0's `[SETUP]`, `[HAPPY-EYEBALLS]`, `[TCP]`, `[UDP]`, `[MULTI]`, `[READ]`, `[WRITE]`, `[TIMER]`, `[SSLS]`, proxy-filter (`[SOCKS]`, `[HTTP-PROXY]`, `[H1-PROXY]`, `[HAPROXY]`) and `[HTTPS-CONNECT]` lines under `--trace-config <name>`, `network`, `all`, and the components `-vv`, `-vvv` and `-vvvv` turn on.

## Context

- ADR-0318 (BL-649) maps these components here; `CommandLineOptions.TraceComponents` holds the names. Measured by BL-649: `-vv` already writes `[SETUP] added`, `[SETUP] happy eyeballing to origin 127.0.0.1:P`, `[SETUP] removing connected setup filter`, `[SETUP] destroy` (and `-vv --trace-config -setup` drops them); `--trace-config all -v` writes the full set in BL-649's Notes, including nanosecond `[PGRS-*] added 404ns` values.
- Many lines report libcurl internals (`pollset`, `multi_wait`, fd numbers, nanosecond progress stamps); write the equivalent from Curl's own connect and transfer code, pin the stable ones byte for byte and record in an ADR amendment how the volatile values (fd, ns) are produced. Split with task-planner first if one run cannot hold it (setup/happy-eyeballs/tcp, then multi/read/write/timer, then proxies).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` for each component name, `network`, `-vv`, `-vvv` and `-vvvv`; stderr in Notes.
- [x] Tests pin each component's lines for a plain HTTP transfer, and that no line appears without its component. (This run: `[SETUP]`; the rest split into BL-1161, BL-1159, BL-1160 as Context allows.)
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1` (`200`, `Content-Length: 2`, `hi`), `-s <args> http://127.0.0.1:P/`; fixtures were in `%TEMP%\bl1103\<case>`. `-v` lines omitted below are plain `-v`'s.
  - `-vv` and `--trace-config setup -v`: `[SETUP] added`, `[SETUP] happy eyeballing to origin 127.0.0.1:P` before `Trying`; `[SETUP] removing connected setup filter`, `[SETUP] destroy` after `Established connection`. Under `-vv` with ids and times; nothing else extra.
  - `-vvv`: as `-vv` plus `[READ] client_reset, clear readers` (before connect, `[0-x]`, and after the body) and the `[WRITE]` lines below; header/data lines become `=> Send header, 79 bytes (0x4f)` hex-style dumps.
  - `--trace-config write -v`: after each `< ` header line `[WRITE] [OUT] wrote 17 header bytes -> 17`, `[WRITE] [PAUSE] writing 17/17 bytes of type c -> 0`, `[WRITE] download_write header(type=c, blen=17) -> 0`, `[WRITE] client_write(type=c, len=17) -> 0` (later headers type 4, preceded by `[WRITE] header_collect pushed(type=1, len=19) -> 0`); after the body `[OUT] wrote 2 body bytes -> 2`, type 1, `xfer_write_resp(len=40, eos=0) -> 0`, then `[WRITE] [OUT] done`.
  - `--trace-config read -v`: `[READ] client_reset, clear readers` before `Trying` and after `{ [2 bytes data]`.
  - `--trace-config timer -v`: `[TIMER] [HAPPY_EYEBALLS] cleared` after `Trying`.
  - `--trace-config tcp -v`: `[TCP] Set TCP_KEEP* on fd=412`, `[TCP] cf_socket_open() -> 0, fd=412`, `[TCP] local address 127.0.0.1 port 58946...`, `[TCP] adjust_pollset, !connected, POLLOUT fd=412`, `[TCP] connected on fd=412` between `Trying` and `Established`; `[TCP] query ALPN` before `using HTTP/1.x`; `[TCP] send(len=79) -> 0, 79` before the request; `[TCP] recv(len=102400) -> 81, 0` and `-> 0, 40` before the response.
  - `--trace-config happy-eyeballs -v`: `init ip ballers for transport 3`, `want to do more`, `check for next AAAA address: none`, `check for next A address: found`, `starting first attempt for ipv4 -> 0`, Trying, `checked connect attempts: 1 ongoing, 0 inconclusive`, `adjust_pollset -> 0, 1 socks`, `connect attempt #0 successful`, `Connected to 127.0.0.1 (127.0.0.1) port P`, Established, `removing connected setup filter`, `destroy`. `localhost`: AAAA found, ipv6 first, `next HAPPY_EYEBALLS timeout in 200ms`, `happy eyeballs timeout expired, start next attempt`, `starting next attempt for ipv4 -> 0`, `connect attempt #1 successful`. Refused: the poll lines repeat, then `checked connect attempts: 0 ongoing`, `no more attempts to try`, `baller 0: result=7`.
  - `--trace-config multi -v`: `[MULTI] [INIT] added to multi, mid=1, running=1, total=2`, `pollset[]`, `multi_wait(...)`, state changes `[INIT] -> [SETUP]` ... `[COMPLETED] -> [MSGSENT]`, `[PGRS-*] set|added <n>ns`, `[CPOOL] added connection 0. The cache now contains 1 members`, `cf_setup_connect [0][!DNS][!SETUP]`, `connected [0][DNS][SETUP][HAPPY-EYEBALLS][TCP]`, `reduced to [0][TCP]`, `xfer_setup: recv_idx=0, send_idx=0`, `multi_done: status: 0 prem: 0 done: 0`, `removed from multi, mid=1, running=0, total=1`.
  - `--trace-config network -v`: `[DNS]`, `[HAPPY-EYEBALLS]`, `[TCP]`, `[MULTI]`, `[TIMER]`; no `[SETUP]`, `[READ]` or `[WRITE]`. `-vvvv`: every component (as `all`) plus ids and times.
  - Order (curl -s ... http://127.0.0.1:1/, grepping `[SETUP]`): `--trace-config -setup -vv` on; `-vv --trace-config -setup` off; `-vv --trace-config -all` off; `-vv --trace-config -network` on; `-vv -v` off; `-vv --no-verbose -v` off; `--trace-config setup -v --no-verbose -v` off; `--trace-config dns -v --no-verbose -v` no `[DNS]`; `--trace-config dns -v -v` `[DNS]` on; `-vv --trace-config setup -v` on; `-vvv --trace-config -read` keeps `[SETUP]` and `[WRITE]`. A failed connect (refused, or `--connect-timeout`) writes only `added` and `happy eyeballing`.
  - Aside, not pinned: `-vv --trace-config setup -v` still showed ids and times.
- Split (the task's Context allows it): this run delivers `[SETUP]` and the verbosity components; `[HAPPY-EYEBALLS]`/`[TCP]` → BL-1161, `[MULTI]`/`[TIMER]`/`[READ]`/`[WRITE]` → BL-1159, proxies and `[HTTPS-CONNECT]` → BL-1160. Decision recorded in ADR-0357.
- Touches: added `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests` - which components `-vv` turns on depends on the order of options (`--trace-config -setup -vv` against `-vv --trace-config -setup`), which only the parser sees. No other task in Doing on `origin/work/dark-factory` named them.
- Delivered: `CommandLineOptions` adds `setup` at `-vv`, `read`/`write` at `-vvv`, `all` at `-vvvv` to `TraceComponents`, a first `-v` takes them out again, `--no-verbose` and `--trace-config -all` empty the set. `SetupFilterTraceEvents` (Networking) writes the `[SETUP]` lines, layered over `DnsFilterTraceEvents` by `TcpConnector.TracingConnectionFilters` under `TracesSetupFilter`; `CurlComposition.TracesSetup` sets it, and `TracesDns` now includes `network`.
- Tests: `CommandLineTraceConfigTests.Parse_VerbosityAndTraceConfig_TurnOnTheComponentsCurlTurnsOn`, `SetupFilterTraceEventsTests`, `TcpConnectorTests.SetupFilterTrace`, and `CurlCompositionDnsTraceTests`' `[SETUP]` cases (alone, with `[DNS]` in curl's order, and absent). `Measure-CodeQuality.ps1`: Curl.Cli, Curl.Networking and Curl.Console at 100% line and branch, no failing member.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -vv and --trace-config setup write curl's [SETUP] lines; -vv..-vvvv turn on their trace components; the rest split into BL-1161..BL-1160
