# ADR-0254 — `TcpConnector` races address families after the happy-eyeballs timeout

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-644.

## Context

`TcpConnector` dialled a host's addresses one at a time in the resolver's order, so a host
whose first family is slow or silent held the connect until that dial failed. curl 8.21.0
dials with happy eyeballs, and `--happy-eyeballs-timeout-ms` (default 200) sets how long the
first family runs alone. BL-644 measured curl 8.21.0 (mingw, Schannel) against `localhost`
(`::1` and `127.0.0.1`) with `Record-CurlExchange.ps1` listening on one family only; the
bytes are in BL-644's Notes:

- `--happy-eyeballs-timeout-ms 50`, IPv4 listening: `Trying [::1]:18644...`, then
  `Trying 127.0.0.1:18644...` 50 ms later; IPv4 wins (`%{remote_ip}` 127.0.0.1,
  `%{time_connect}` 0.055 s) and the abandoned IPv6 attempt prints no line.
- `5000`, IPv4 listening: `::1` is refused after about 2 s (Windows retries the SYN), its
  `connect to ::1 port 18644 from :: port 0 failed: Connection refused` line is printed, and
  `127.0.0.1` is tried at once (`%{time_connect}` 2.02 s).
- `0`: both `Trying` lines at once. No option: IPv4 wins after about 200 ms.
- The option reads a C `long` of whole milliseconds as `--keepalive-cnt` does: `-1` is
  "expected a positive numerical parameter"; `abc`, `1.5`, `+5`, `0x10`, `1e3`, ` 7`, the
  empty value and `2147483648` on Windows are "expected a proper numerical parameter";
  `--no-happy-eyeballs-timeout-ms` "cannot be reversed with a --no- prefix".

## Decision

1. **`AddressFamilyRace` in `Curl.Networking.UnitLibrary` does the dialling.** The first
   address's family is dialled in turn, one address at a time; the other family is
   dialled the same way beside it, started once the timeout has passed on the injected
   `TimeProvider` or as soon as every address of the first family has failed, whichever
   comes first. The first connection made wins. The attempts still running are cancelled,
   and one that connects anyway is closed; neither prints a line. When every address fails,
   the `SocketError` of the attempt that failed last is kept, as before, for
   `--retry-connrefused`.
2. **One loop starts every attempt and reports every outcome**, so the `-v` lines and the
   diagnostic log come one at a time in the order curl prints them, with no locking.
3. **`TcpConnector` takes `happyEyeballsTimeout`**, `null` for curl's 200 ms
   (`TcpConnector.DefaultHappyEyeballsTimeout`), held to between zero and the longest delay
   a .NET timer takes (about 49.7 days). Proxies are raced the same way, as curl races them.
4. **The CLI records `CommandLineOptions.HappyEyeballsTimeout`** per option group through
   `CommandLineNumber.ParseMilliseconds`: `ParseNonNegative` over the platform's `LONG_MAX`,
   capped at about `TimeSpan.MaxValue`, which only a 64-bit `long` reaches.
5. **Composing it is BL-889's.** `Curl.Console` builds the connector and the HTTP/3 race;
   BL-889 passes the parsed value to both.

## Consequences

- Within one family curl 8.21.0 may also start the next address before the previous one
  fails; this build waits for the failure. A host with several unreachable addresses of one
  family connects later than curl would, never with a different outcome. Revisit when a
  measurement shows the difference.
- Tests drive the race with `GatedTcpDialer` and `ManualTimeProvider`, pinning when each
  dial starts and which connection wins, with no network.
