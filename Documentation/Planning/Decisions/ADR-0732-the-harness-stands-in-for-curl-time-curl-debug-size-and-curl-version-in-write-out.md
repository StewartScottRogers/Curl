# ADR-0732 — The harness stands in for CURL_TIME, CURL_DEBUG_SIZE and CURL_VERSION in the curl tool's -w output

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2909
- Decided by Claude under Stewart's delegation.

## Context

Upstream cases 970 and 972 (`--write-out '%{json}'`) and 1981 (`%time{...}`) need the Debug
feature: curl's debug build reads three test variables that change what `-w` prints.

- `CURL_TIME` (`src/tool_writeout.c`, `outtime`): `%time{}` formats that many seconds after the
  epoch, with `CURL_TIME % 1000000` as the microseconds `%f` prints. In `lib/getinfo.c` it also
  makes `curl_easy_getinfo` answer the number for `CURLINFO_LOCAL_PORT`, every `*_TIME_T` (as
  microseconds) and both `SPEED_*_T`.
- `CURL_DEBUG_SIZE` (`lib/getinfo.c`): the number for `CURLINFO_HEADER_SIZE` and
  `CURLINFO_REQUEST_SIZE`.
- `CURL_VERSION` (`lib/version.c`): what `curl_version()` returns, which `%{json}` prints last as
  `curl_version`.

`%Z` and `%z` are rewritten to `UTC` and `+0000` by `outtime` itself, before `strftime`, so case
1981's expected line is the same on every platform.

## Decision

`Curl.Console`'s `DebugWriteOutStandIn` holds the three values and lays them over a transfer's
`-w` values: `ClockOr` gives the `%time{}` clock (a `FixedWallClock`, whose timestamps stay the
system's, so transfer timings and waits are untouched), `Apply` the getinfo answers and
`LibraryVersionOr` the version. `CurlCommandRunner` takes one (`debugWriteOut`); only the dialing
`CurlComposition.CreateRunner(honoursDebugWriteOutVariables: true)`, which the conformance harness
calls, builds it from the environment, so the shipped `curl` never reads the variables, as curl's
release build does not. The harness reports Debug to a case setting `CURL_VERSION` or
`CURL_DEBUG_SIZE`, or `CURL_TIME` without naming HSTS (the HSTS cases keep their own rule).

A `CURL_TIME` that is not a whole number up to 253402300799 (the last second `DateTimeOffset`
holds) is not honoured. The stand-in changes only `-w` output: the libtest engine's getinfo is
not changed, as no libtest case on the ratchet needs it.

## Consequences

Cases 970, 972 and 1981 pass and are on the ratchet. The HSTS cases that set `CURL_TIME` use no
`-w`, so their output is unchanged.
