# ADR-0106 — The runner watches each attempt for low speed and cancels it

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-400.

## Context

BL-196 parsed `-Y`/`--speed-limit` and `-y`/`--speed-time` into `CommandLineOptions`, but
nothing acted on them. libcurl 8.21.0's `Curl_speedcheck` runs once a second: while the
current speed (the faster direction, over the last five seconds of progress samples) is below
the limit it keeps the time the slow spell began, and once the spell has lasted the speed
time it fails the transfer with `CURLE_OPERATION_TIMEDOUT`.

BL-400 measured curl 8.21.0 (Windows, Schannel) with `Record-CurlExchange.ps1` against a
server that holds its response back:

| Command line | stderr | Exit |
| --- | --- | ---: |
| `-sS -Y 100 -y 2` | `curl: (28) Operation too slow. Less than 100 bytes/sec transferred the last 2 seconds` | 28 |
| `-sS -Y 100` | `... Less than 100 bytes/sec transferred the last 30 seconds` | 28 |
| `-sS -y 2` | `... Less than 1 bytes/sec transferred the last 2 seconds` | 28 |
| `-sS -Y 0 -y 2` | `... Less than 1 bytes/sec transferred the last 2 seconds` | 28 |

The task text said `-y` without `-Y` sets no limit; the measurement says it watches for
1 byte per second, as BL-196's `--libcurl` reading and `CommandLineOptions.SpeedTimeSeconds`
already record. The measurement wins.

Only the HTTP and FTP handlers report progress, and handlers take no speed options, so a
check inside each handler would leave most schemes unwatched and touch every protocol.

## Decision

- **`Curl.Core`'s `LowSpeedWatchdog` does the check**, on a timer from the runner's
  `TimeProvider`, as `Curl_speedcheck` does: a sample a second, speed over the last six
  samples, the faster of the bytes written to the attempt's output and the bytes its handler
  reported uploaded, a slow spell from the first slow check, and a trip once the spell lasts
  the speed time.
- **The runner starts one per attempt** (`TransferContextFactory` wraps the output and the
  progress sink and gives the context the watchdog's token as its `CancellationToken`) and
  stops it when the attempt ends. An attempt that throws `OperationCanceledException` after
  the watchdog tripped ends with exit 28 and curl's message, which `--retry` counts as
  transient like any timeout; any other cancellation still propagates.
- **Defaults are applied to what was given**: `-Y` without `-y` watches for 30 seconds, `-y`
  without `-Y` for 1 byte per second, and a limit or time of zero watches nothing.

## Consequences

- Every scheme is watched without a handler change, as long as its handler honours
  `ITransferContext.CancellationToken`.
- The download speed counts every byte written to the body output, so `-i` header lines
  count too; curl counts body bytes only. The difference only matters within a second of the
  limit.
- `CommandLineOptions` keeps the last value of each option, not the order they came in, so
  `-Y 0 -y 2` watches nothing while curl, whose `-y` sets a zero limit to 1 as it is parsed,
  watches for 1 byte per second; and `-Y 100 -y 0` watches nothing, as curl does. Matching
  the first needs the parser to apply the defaults in order.
