---
id: BL-013
title: Implement --max-filesize and shared range parsing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-007, BL-037]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Documentation/Product/Requirements.md]
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-013 — Implement `--max-filesize`, plus range parsing shared across protocols

## Goal

`--max-filesize` works, and `-r`/`--range` is parsed once into the `ByteRange` type
from ADR-0003 so every protocol handler receives an already-validated range.

## Context

A Core concern: parsing happens once, and handlers read `ITransferContext.Range`
instead of parsing it themselves. See
`Documentation/Planning/Decisions/ADR-0003-itransfercontext-carries-transfer-options.md`.
Exit codes: https://curl.se/libcurl/c/libcurl-errors.html, checked against
curl 8.21.0.

## Acceptance criteria

- [x] `-r`/`--range` is parsed into `ByteRange` in one place, and handlers receive it
      through `ITransferContext.Range`.
- [x] A range that cannot be satisfied returns exit 33 (`CURLE_RANGE_ERROR`).
- [x] `--max-filesize` is implemented, with the exit code upstream curl returns when
      it is exceeded.
- [x] `-r 3-1`, `-r abc` and `-r -0` each return exit 33 (`CurlExitCode.RangeError`)
      with the message `Requested range was not delivered by the server`, as measured
      against curl 8.21.0. `ByteRange.Bounded` today throws
      `ArgumentOutOfRangeException` for a reversed range, and `ByteRange.Suffix` throws
      for a zero suffix, and nothing maps either exception to exit 33: the parser rejects
      these before a `ByteRange` is constructed, and a test asserts each of the three.
- [x] `-r` together with `-C`/`--continue-at` is refused before any transfer, with exit 2
      (`CurlExitCode.FailedInit`) and the message
      `curl: --continue-at is mutually exclusive with --range`. Upstream makes them
      mutually exclusive — "This command line option is mutually exclusive with --range"
      (<https://curl.se/docs/manpage.html>, curl 8.21.0) — so
      `FileProtocolHandler.TryResolveWindow`'s current "`-C` wins over `-r`" precedence
      describes a case curl never reaches. A test pins the refusal.

## Notes

Measured against curl 8.21.0 on 2026-09-25: the `-C` with `-r` refusal prints **three**
stderr lines, not one, and exits 2:

```
curl: --continue-at is mutually exclusive with --range
curl: option -C: is badly used here
curl: try 'curl --help' or 'curl --manual' for more information
```


The original item did not name `--max-filesize`'s exit code. Confirm it from the
upstream error list during planning.

### Delivered (2026-09-26, dark factory lane 4)

- **Where the range is parsed.** `Curl.Cli` keeps the `-r` text as curl's tool keeps it
  (`CommandLineOptions.Range`); `Curl.Core.ByteRangeParser.TryParse` is the one place it
  becomes a `ByteRange`, following libcurl's `Curl_range`, and `NotDeliveredFailure` is
  exit 33 `Requested range was not delivered by the server`. Chosen because the exit 33
  is a transfer-time failure upstream (after the progress meter), not a command-line
  refusal, and because an HTTP handler will later need the raw text.
- **Measured on curl 8.21.0 (file://, 2026-09-26):** `3-1`, `abc`, `-0`, `a-3`, ` 1-2`,
  `+1-2`, `99999999999999999999`, `0-9223372036854775807` exit 33; `2-3,5-6` is `2-3`,
  `1-2abc` is `1-2`, `5-abc` and `3--1` run to the end, `-` and `-99999…` are the whole
  file, `-3-1` is the last 3 bytes. `-r 5`/`5abc`/`5,6` warn (two lines) and become `5-`;
  a non-digit/dash/comma character warns (three lines). An earlier `-s` hides both warnings.
- **`-r` with `-C`:** refused by whichever option comes second (`option -C:` or `option -r:`,
  as typed), before its value is checked. The `mutually exclusive` line is hidden when `-s`
  without `-S` came first; the other two lines always print.
- **`-C` row added** because the refusal needs it. Its value check (unsigned digits fitting a
  64-bit offset, `-` = from output size; `-0`, `-1`, `abc`, `1e3`, `1k`, empty all
  `expected a proper numerical parameter`) overlaps BL-027. BL-027 keeps the handler-branch
  decision; a log line there says so.
- **`--max-filesize`:** exit 63 (`CURLE_FILESIZE_EXCEEDED`), message
  `Exceeded the maximum allowed file size (N) with N bytes`. curl 8.21.0 writes exactly N body
  bytes first, does not count headers (`-i`), ignores it for `-I` and uploads, counts from the
  `-r`/`-C` window, and treats 0 as no limit. Values take `b/k/m/g/t/p` units and fractions
  (fraction truncated to 3/6/9/12/15 digits per unit), measured through `--libcurl`.
- **`ITransferContext.MaxFileSize`** (`long?`) added to the shared contract (ADR-0003's
  family of options). Only `file://` enforces it. The ADR itself is not amended:
  `Documentation/Planning/Decisions` was not in this task's `touches`.
- **Not done here, filed:** BL-090 wires `Range`, `ResumeFrom` and `MaxFileSize` into
  `Curl.Console`'s `CurlCommandRunner.CreateContext` (not in `touches`; until then the console
  accepts the three options and ignores them, and prints no parse warnings). BL-091 makes the
  existing `-o` looks-like-a-flag warning respect `-s`, a divergence found while measuring.
- **Quality gate:** `Measure-CodeQuality.ps1` shows no failing member added by this task; every
  failure it lists in the four libraries predates it. Run it with a lane-private
  `-ResultsDirectory`: the default `%TEMP%\CurlCodeQuality` is shared by every lane.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-25: Amended from the file:// conformance audit against curl 8.21.0: added the
  `-r 3-1`, `-r abc` and `-r -0` exit 33 cases and the `-r` with `-C` exit 2 refusal.
- 2026-09-25: Recorded all three stderr lines of the -C-with-r refusal; the criterion named only the first.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Dark factory shift stopped by Stewart before work began; returned unchanged.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Backlog. Returned when the 4-lane shift was stopped to repair task IDs that parallel lanes had duplicated; no lane was working it.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -r is parsed once into ByteRange by Core's ByteRangeParser (exit 33 for 3-1, abc, -0), -r with -C is refused with exit 2, and --max-filesize stops a file:// download at the limit with exit 63
