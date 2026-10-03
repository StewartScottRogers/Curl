---
id: BL-1191
title: Write the [SOCKS] trace lines of a SOCKS proxy and --preproxy for --trace-config
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1191 — Write the [SOCKS] trace lines of a SOCKS proxy and --preproxy for --trace-config

## Goal

Curl writes curl 8.21.0's `[SOCKS]` lines for a SOCKS4, SOCKS4a, SOCKS5 and SOCKS5h proxy and for `--preproxy` under `--trace-config socks`, `proxy`, `all` and `-vvvv`.

## Context

- Split from BL-1160 (ADR-0357's BL-1160 amendment), which delivered the [HAPROXY] lines and pinned [SETUP] through a plain HTTP proxy. The handshake runs in `SocksProxyTunnel` from `TcpConnector.ConnectThroughProxyAsync`; `Record-CurlExchange.ps1 -Script` can play the SOCKS server byte for byte.
- Measure first with `Record-CurlExchange.ps1`; extend it to play the proxy if it cannot (it serves one HTTP exchange, so a plain `-x` proxy works already). Each line's place relative to `[SETUP]`, `[DNS]`, `Established connection` and the CONNECT lines matters; [HAPPY-EYEBALLS], [TCP], [MULTI] and [TIMER] are other tasks.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (a successful transfer and a refused connect), stderr in Notes; which `--trace-config` groups (`proxy`, `network`, `all`) turn the lines on is measured too.
- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the stable lines and that no line appears without its component; ADR-0357 gets an amendment for any volatile value.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

Measured 2026-10-02 with `Record-CurlExchange.ps1 -Script` playing the SOCKS server (curl 8.21.0 Schannel, Windows). `-s -v --trace-config socks -x socks5h://127.0.0.1:18601 http://example.test/x`, exit 0:

```
*   Trying 127.0.0.1:18601...
* [SOCKS] SOCKS5: connecting to example.test:80
* [SOCKS] adjust pollset in (7)
* [SOCKS] SOCKS5 connect to example.test:80 (remotely resolved)
* [SOCKS] adjust pollset in (15)
* [SOCKS] SOCKS5 request granted.
* Opened SOCKS connection from 127.0.0.1 port 58631 to example.test port 80 (via 127.0.0.1 port 18601)
* Established connection to 127.0.0.1 (127.0.0.1 port 18601) from 127.0.0.1 port 58631
* [SOCKS] query ALPN
* using HTTP/1.x
```

- `socks5://` to `127.0.0.1`: `SOCKS5 connect to 127.0.0.1:80 (locally resolved)`; to `localhost`: `SOCKS5 connect to [::1]:80 (locally resolved)`; `socks5h://` to `127.0.0.1`: `SOCKS5 connect to 127.0.0.1:80 (remotely resolved)`.
- `socks4://` to `127.0.0.1` or `localhost`: `SOCKS4 connecting to H:80`, `SOCKS4 connect to IPv4 127.0.0.1 (locally resolved)`, `adjust pollset in (4)`, `SOCKS4 request granted.`; `socks4a://`: `SOCKS4a connecting to example.test:80`, `adjust pollset in (4)`, `SOCKS4a request granted.`
- Refused connect, SOCKS5h answering `05 05`, exit 97: the lines stop after `adjust pollset in (15)`, then `* cannot complete SOCKS5 connection to example.test. (5)`. SOCKS4 answering `5b`, exit 97: after `adjust pollset in (4)`, `* [SOCKS] cannot complete SOCKS4 connection to 127.0.0.1:80. (91), request rejected or failed.`, which plain `-v` prints too (it is curl's error message, already written).
- `--preproxy socks5h://127.0.0.1:18601 -x http://proxy.test:3128`: the same lines naming `proxy.test:3128`.
- Groups: `socks`, `proxy` and `all` write them; `network` does not; `-vvvv` does not either (it writes `[SETUP] added SOCKS filter to 127.0.0.1:80` but no `[SOCKS]` line), although it puts `all` among the components. `--trace-config all` adds `[SETUP] added SOCKS filter to example.test:80` after `[HAPPY-EYEBALLS] Connected to` and before `[SOCKS] SOCKS5: connecting`.

Decisions (ADR-0357's BL-1191 amendment):
- The bracketed numbers are curl's handshake states, the same on every run, so they are pinned.
- To tell `-vvvv`'s `all` from `--trace-config all`, `CommandLineOptions` exposes `VerbosityTraceComponents`; so `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests` joined `touches` (no task in Doing named them).
- `[SETUP] added SOCKS filter to H:P` is written too, since it is measured and orders the lines under `all`.
- `[SOCKS] query ALPN` is filed as BL-1246 (the HTTP layer's ALPN query, beside BL-1195's `[TCP] query ALPN`). SOCKS5 user name and password and GSS-API lines are unmeasured; the lines are written around them where the state machine puts them.
- `Measure-CodeQuality.ps1`: Curl.Networking.UnitLibrary and Curl.Cli.UnitLibrary 0 failing members. Curl.Console reports one, `CurlCommandRunner.TransferUrlAsync` at complexity 12, which this task did not touch; filed as BL-1247. Curl.Console's new `TracesSocks` is fully covered.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. SOCKS4, SOCKS4a, SOCKS5, SOCKS5h and --preproxy handshakes write curl 8.21.0's [SOCKS] lines under --trace-config socks, proxy and a named all
