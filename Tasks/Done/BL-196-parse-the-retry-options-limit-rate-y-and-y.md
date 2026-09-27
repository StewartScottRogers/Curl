---
id: BL-196
title: Parse the --retry options, --limit-rate, -Y and -y
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-053]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-196 — Parse the --retry options, --limit-rate, -Y and -y

## Goal

`--retry`, `--retry-delay`, `--retry-max-time`, `--retry-all-errors`, `--retry-connrefused`, `--limit-rate`, `-Y`/`--speed-limit` and `-y`/`--speed-time` parse into `CommandLineOptions`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item C10. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-053 decides the numeric ceiling these obey.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `--limit-rate` accepts the k, M and G suffixes (case as measured) and refuses others as measured.
- [x] Numeric values follow the BL-053 ceiling and FR-049/FR-050 refusals.
- [x] Every refusal and warning this option group can raise exits 2 with the exact lines measured on curl 8.21.0, and every `--no-` spelling curl accepts for these options is measured and tested (a test per spelling).
- [x] `dotnet build Curl.Cli.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Cli`.

## Notes

- Plan item: C10 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Measured 2026-09-26 against `/mingw64/bin/curl` 8.21.0 (Schannel) with `curl <option> <value> --libcurl lc.c file:///Z:/nonexistent_bl196`: accepted values exit 37, and `lc.c` shows `CURLOPT_MAX_RECV_SPEED_LARGE`/`MAX_SEND_SPEED_LARGE`, `CURLOPT_LOW_SPEED_LIMIT` and `CURLOPT_LOW_SPEED_TIME`; refused ones exit 2 with `curl: option <spelled>: <reason>` and the try-help line. A `file:` URL was used instead of a loopback server because no transfer is needed to see whether a value parses, and a file error is never retried without `--retry-all-errors`.
- Findings: every option in the group reads exactly as an existing `CommandLineNumber` reader does, so no new reader was written. `--retry`, `-Y`, `-y`: `ParseNonNegative` at the platform `LONG_MAX` (BL-053/ADR-0019: `2147483647` accepted, `2147483648` refused as not a proper number on Windows; `-1` "expected a positive numerical parameter"; `-0` accepted). `--retry-delay`, `--retry-max-time`: `ParseSeconds` (curl's `secs2ms`: `1.5`, `1,5`, `1e3`, `0x10` accepted; `-0`, `-1`, `.5`, `2147483` refused as not a proper number; `1.99999999999999999999` "too large number"). `--limit-rate`: `ParseSize`, identical to `--max-filesize`: `b k m g t p` in either case, fractions (`1.5k` = 1536, `0.3333k` = 340, `8191.99999p` = 9223372025595734116); `1x`, `1kb`, `1k/s`, `1.5`, `1.5b`, `0x10` "is badly used here"; `.5k`, `1.k`, `-1`, `-0` not a proper number; `8589934592G` and `9223372036854775808` "too large number".
- `--no-` spellings: `--no-retry-all-errors` and `--no-retry-connrefused` (and `=x`) are accepted and turn the flag off; `--no-retry`, `--no-retry-delay`, `--no-retry-max-time`, `--no-limit-rate`, `--no-speed-limit`, `--no-speed-time` (and each `=x`) are "cannot be reversed with a --no- prefix". No option in the group raises a warning in any case measured.
- Choices (defaults taken, no ADR needed since each follows measured curl): `RetryCount` is a `long` defaulting to 0 (curl's default); `SpeedTimeSeconds` is a `long?` of whole seconds, not a `TimeSpan`, because on Linux the 64-bit ceiling does not fit a `TimeSpan`; `LimitRate` is one value for both directions, as curl sets both. The transfer-side defaults curl applies (`-Y` alone uses 30 s; `-y` alone uses 1 byte/s) are documented on the properties for the layer that consumes them, not applied at parse time.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --retry, --retry-delay, --retry-max-time, --retry-all-errors, --retry-connrefused, --limit-rate, -Y and -y parse as curl 8.21.0 does
