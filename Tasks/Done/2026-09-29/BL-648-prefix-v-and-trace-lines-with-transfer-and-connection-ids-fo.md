---
id: BL-648
title: Prefix -v and --trace lines with transfer and connection IDs for --trace-ids
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-648 — Prefix -v and --trace lines with transfer and connection IDs for --trace-ids

## Goal

`--trace-ids` parses and prefixes every `-v`, `--trace` and `--trace-ascii` line with curl 8.21.0's `[<xfer>-<conn>]` marker (with its form before a connection exists), byte for byte.

## Context

- Conformance audit 2026-09-28, row 30 (Major).
- Formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`, `TraceTimeStamp.cs` (`--trace-time` is the precedent for a per-line prefix and its position relative to the time). IDs come from `Curl.Console` (ADR-0060: `%{xfer_id}` and `%{conn_id}`).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Connections 2`: `-v --trace-ids` for two URLs, `--trace-ascii - --trace-ids`, and `-v --trace-ids --trace-time`; stderr (or the trace output) copied into Notes with varying parts marked.
- [x] Tests pin each measured output, normalised as existing `-v` tests are; `Curl.Cli.UnitTests` pin parsing.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-29 against local curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`
serving `HTTP/1.1 200 OK`, `Content-Length: 2`, `Connection: close`, `hi`. Varying parts: `<port>`
(local port), `<t>` (time of day). Progress-meter lines left out.

`-v --trace-ids http://127.0.0.1:48648/a http://127.0.0.1:48648/b` (`-Connections 2`), stderr:

```
[0-0] *   Trying 127.0.0.1:48648...
[0-0] * Established connection to 127.0.0.1 (127.0.0.1 port 48648) from 127.0.0.1 port <port> 
[0-0] * using HTTP/1.x
[0-0] > GET /a HTTP/1.1
[0-0] > Host: 127.0.0.1:48648
[0-0] > User-Agent: curl/8.21.0
[0-0] > Accept: */*
[0-0] > 
[0-0] * Request completely sent off
[0-0] < HTTP/1.1 200 OK
[0-0] < Content-Length: 2
[0-0] < Connection: close
[0-0] < 
[0-0] { [2 bytes data]
[0-0] * shutting down connection #0
[1-1] * Hostname 127.0.0.1 was found in DNS cache
[1-1] *   Trying 127.0.0.1:48648...
... every line of the second transfer marked [1-1] ...
[1-1] * shutting down connection #1
```

`--trace-ascii - --trace-ids`, stdout: `[0-0] => Send header, 80 bytes (0x50)` then
`0000: GET /a HTTP/1.1` ... unmarked; every `* ` and `=>`/`<=` title line marked `[0-0] `.

`-v --trace-ids --trace-time`: `<t> [0-0] *   Trying 127.0.0.1:48648...` - the stamp first, then
the IDs. `--trace - --trace-ids --trace-time` the same, hex lines unmarked.

The form before a connection exists, `[<xfer>-x]`:
- `-v --trace-ids --resolve a.test:1:127.0.0.1 http://a.test:1/`: `[0-x] * Added a.test:1:127.0.0.1 to DNS cache`, then `[0-0] * Hostname a.test was found in DNS cache` ...
- `-v --trace-ids xyz://a/ http://127.0.0.1:1/`: `[0-x] * Protocol "xyz" not supported`, then the second transfer's lines `[1-0]` (xfer 1, conn 0).
- `-v --trace-ids "http://a b/"`: `[0-x] * URL rejected: Malformed input to a URL function`.

Parsing (first two lines of stderr): `--trace-ids -v` no IDs (a first-option `-v` resets it, as it
does `--trace-time`); `--trace-ids -sv` IDs; `-vv` stamp and IDs (plus `[SETUP]` component lines,
BL-649's); `-vvv --no-trace-ids` no IDs; `-vvv -v` neither.

Decisions (ADR-0202): each transfer reports through a `TraceIdsTransferEvents` view that sets the
shared `TraceIdsPrefix` under a lock, so `-Z` transfers never swap IDs; a transfer takes its
`%{conn_id}` at its first event, and `-w` prints that number. Our runner writes no `-v` line for
a rejected URL or unsupported scheme yet, so `[n-x]` shows only on the `--resolve`/`-b` lines;
not measured: connection reuse (the recorder closes each connection).

Added `Documentation/Planning/Decisions` to `touches` for ADR-0202; no task in Doing names it.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. --trace-ids and -vv prefix every -v, --trace and --trace-ascii line with curl 8.21.0's [xfer-conn] marker, [xfer-x] before a connection
