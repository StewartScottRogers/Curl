# ADR-0089 — A `-z` time past Windows' `gmtime` fails the HTTP transfer with exit 43

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-381.

## Context

ADR-0073 reads a `-z` date after the year 9999 as 9999-12-31 23:59:59 UTC, because
`TimeCondition.Value` is a `DateTimeOffset`, and so the HTTP handler sent
`If-Modified-Since: Fri, 31 Dec 9999 23:59:59 GMT` for `-z "1 Jan 099999999"`.

BL-381 measured curl 8.21.0's Schannel build (Git for Windows, `x86_64-w64-mingw32`) with
`Record-CurlExchange.ps1` and `-sS -z <date> http://127.0.0.1:<port>/`:

- `1 Jan 3001` sends `If-Modified-Since: Thu, 01 Jan 3001 00:00:00 GMT`, exit 0.
- `1 Jan 3001 20:59:59 GMT` is the last time sent; `1 Jan 3001 21:00:00 GMT`, `1 Jan 4000`,
  `1 Jan 9999`, `31 Dec 9999 23:59:59` and `1 Jan 099999999` all fail with exit 43 and
  `curl: (43) Invalid TIMEVALUE`, after connecting and before sending a byte; `-v` reports
  the connection `left intact`. `-z -<date>` and an `-H "If-Modified-Since: x"` override
  fail the same way.

The boundary is the Windows C runtime's: `gmtime` refuses a time past `_MAX__TIME64_T`
(3001-01-01 07:59:59 UTC) plus its 13-hour `_MAX_LOCAL_TIME`, Unix time 32535291599. It
does not depend on the time zone. The OpenSSL build's `gmtime` takes every time a
`TimeCondition` holds.

## Decision

- **On Windows, an HTTP transfer whose `-z` time is after 3001-01-01 20:59:59 UTC fails with
  exit 43 `Invalid TIMEVALUE`** once connected, sends nothing, and leaves the connection
  reusable (`HttpTimeConditionLimit`, checked in `HttpProtocolHandler`).
- Elsewhere the header is written as before. `TimeCondition` is not widened: every date
  ADR-0073 clamps is past the Windows limit, so the clamp only shows on Linux and macOS,
  and only for a year after 9999.

## Consequences

`-z` over HTTP now matches curl on Windows for every date. On Linux and macOS a date after
the year 9999 still sends the clamped `Fri, 31 Dec 9999 23:59:59 GMT`, where the OpenSSL
build would write the real year; unmeasured, and left as ADR-0073 left it.

## Alternatives considered

- Widen `TimeCondition` to a 64-bit Unix time (superseding ADR-0073): needed only to print
  years past 9999 on Linux and macOS, which is unmeasured; it would touch
  `Curl.Protocol.Abstractions.UnitLibrary` and every protocol.
- Fail while building the request head: the check sits before the exchange instead, so the
  connection is left intact as curl leaves it.
