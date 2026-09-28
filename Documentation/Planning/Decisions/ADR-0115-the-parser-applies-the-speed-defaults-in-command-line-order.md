# ADR-0115 — The parser applies the speed defaults in command-line order

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-470.
Amends ADR-0106.

## Context

ADR-0106 left `-Y 0 -y 2` watching nothing, because `CommandLineOptions` keeps only the last
value of `-Y`/`--speed-limit` and `-y`/`--speed-time` and `LowSpeedWatchdog.StartFromCommandLine`
applies the defaults to an option that was not given. curl 8.21.0's tool applies them as it
parses: `-Y` sets a zero speed time to 30, and `-y` sets a zero speed limit to 1, so the
result depends on the order the options came in.

BL-470 measured curl 8.21.0 (Windows, Schannel) with `Record-CurlExchange.ps1` against a
server that sends a head promising 100 bytes, 3 of them, and then holds the connection open
for 6 seconds:

| Command line | stderr | Exit |
| --- | --- | ---: |
| `-sS -Y 0 -y 2` | `curl: (28) Operation too slow. Less than 1 bytes/sec transferred the last 2 seconds` | 28 |
| `-sS -y 2 -Y 0` | `curl: (18) end of response with 97 bytes missing` | 18 |
| `-sS -Y 100 -y 0` | `curl: (18) end of response with 97 bytes missing` | 18 |
| `-sS -y 0 -Y 5` | `curl: (18) end of response with 97 bytes missing` (a 30-second watch outlasts the server) | 18 |

## Decision

- **The `-Y` and `-y` appliers in `CommandLineOptionTable` apply curl's defaults as they
  parse**: `-Y` makes a `SpeedTimeSeconds` of 0 into 30, and `-y` makes a `SpeedLimit` of 0
  into 1. An option not yet given stays `null`, so `LowSpeedWatchdog.StartFromCommandLine`
  keeps defaulting a missing one, and `Curl.Core` is unchanged.
- The watchdog still watches nothing when the final limit or time is zero, which is how curl
  treats `-y 2 -Y 0` and `-Y 100 -y 0`.

## Consequences

- `-Y 0 -y 2` watches for 1 byte per second over 2 seconds, and `-y 0 -Y 5` for 5 bytes per
  second over 30 seconds, as curl does.
- `CommandLineOptions.SpeedLimit` and `SpeedTimeSeconds` are no longer simply the last value
  given; their doc comments say when they are not.

## Alternatives considered

- **Record the order in `CommandLineOptions` and apply the defaults in `LowSpeedWatchdog`**:
  spreads one rule of curl's parser across two projects and a new property, where the
  appliers can apply it at the moment curl does.
