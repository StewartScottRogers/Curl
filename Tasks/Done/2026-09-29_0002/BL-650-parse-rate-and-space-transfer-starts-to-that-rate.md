---
id: BL-650
title: Parse --rate and space transfer starts to that rate
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-650 — Parse --rate and space transfer starts to that rate

## Goal

`--rate <N/unit>` (`s`, `m`, `h`, `d`, with an optional multiplier such as `2/3s`) parses with curl 8.21.0's checks and makes the runner wait between transfer starts so no more than that many start per period, as curl does for serial transfers.

## Context

- Conformance audit 2026-09-28, row 30 (Major).
- The per-URL loop is `Curl.Console/CurlCommandRunner.cs`; wait on the injected `TimeProvider`, never `Thread.Sleep`. Whether the wait counts from the previous start or its end, and whether `-Z` ignores `--rate`, must be measured.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Connections 3`: `--rate 2/s` and `--rate 1/3s` with three URLs (the script reports curl's run time), and `--rate 0`, `--rate 1/x`, `--rate abc`; timings, stderr and exit codes copied into Notes.
- [x] `Curl.Cli.UnitTests` pin parsing and refusals; `Curl.Console.UnitTests` on a fake `TimeProvider` pin when each transfer starts.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-29 with the local curl 8.21.0 (mingw, Schannel); stderr lines below end in the mingw build's
`curl: try 'curl --help' for more information`, which the tests pin as the project's `TryHelpLine`.

- `-v --rate 2/s` over three URLs (`Record-CurlExchange.ps1 -Connections 3`): exit 0 after 1318 ms, with
  `Note: Transfer took 46 ms, waits 454ms as set by --rate` and `Note: Transfer took 6 ms, waits 494ms as set by --rate`
  between the transfers - the wait counts from the previous transfer's **start**, and none follows the last.
- `-s --rate 1/3s` over three URLs: exit 0 after 6124 ms. `-s -Z --rate 1/3s`: 145 ms - `-Z` ignores `--rate`.
- The Note is written under `-v` or `--trace-ascii`, with or without `-s`, never without them. A failed transfer
  (`file:///nonexistent/x`, exit 37) is waited after too. `--rate 2/s` given after `--next` spaced all three transfers (1058 ms): global.
- Refusals, exit 2: `0`, `0/s`, `0x10/s` "is badly used here"; `abc`, `""`, ` 2/s`, `+2/s`, `-1/s`,
  `99999999999999999999/s`, `9223372036854775808/d` "expected a proper numerical parameter"; `1/x`, `2/3`, `2/`,
  `2/ s`, `2/S`, `1/-1s`, `5/0x2s`, `1/99999999999999999999s`, `1/2562047788015x` `curl: unsupported --rate unit` then
  badly used; `1/9223372036854776s`, `1/106751991168d` `curl: too large --rate unit` then "too large number";
  `1/2562047788016x` both messages then too large; `1001/s`, `1/0s`, `3601000/h`, `2147483647/s` "too large number".
  `-s` hides the `unsupported`/`too large --rate unit` lines, `-sS` keeps them.
- Accepted (exit 7 against a closed port): `2` (per hour), `2x`, `1x/s`, `2 /s`, `010/s`, `2/sx`, `1/sm`, `1/s/s`,
  `1/1001s`, `1/25d` (so 64-bit arithmetic even on Windows), `1/9223372036854775s`, `1/106751991167d`, `3600000/h`,
  `1000/s`, leading zeros of any length. So: the leading digits of N are read, `/` is found anywhere, M's digits are
  read when they fit a long (else M=1 and the unit is read where M began), and anything after the unit letter is ignored.

Decisions (sensible defaults, no ADR needed since each follows the measurement):
- The interval is a `long` of milliseconds (`CommandLineOptions.MillisecondsBetweenTransferStarts`), not a `TimeSpan`,
  because curl accepts periods up to `long.MaxValue` ms, beyond `TimeSpan`'s range; the runner waits in pieces of at most
  `int.MaxValue` ms, which `Task.Delay` allows.
- The wait runs before the next serial transfer starts rather than after the previous one ends; the same thing as
  curl's "if there is a next transfer" check, without looking ahead in the URL/glob loops.
- A retried transfer is measured from its first attempt's start, where curl resets `start` on every retry:
  filed as BL-971. `--ai-help` needed no edit: it is generated from `CommandLineOptionTable`, so `--rate` lost its
  "Not supported by this build yet" line with the new row.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --rate parses as curl 8.21.0 does and spaces serial transfer starts on the injected clock, with the -v Note
