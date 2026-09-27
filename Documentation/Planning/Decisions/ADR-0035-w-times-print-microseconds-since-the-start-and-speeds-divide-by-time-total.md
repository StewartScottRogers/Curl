# ADR-0035 — `-w` times print microseconds since the start, and speeds divide by `time_total`

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-226 makes `TransferWriteOutVariables` print `%{time_namelookup}`, `%{time_connect}`,
`%{time_appconnect}`, `%{time_pretransfer}`, `%{time_posttransfer}`,
`%{time_starttransfer}`, `%{time_redirect}`, `%{time_total}`, `%{speed_download}` and
`%{speed_upload}` from `TransferReport.Timings` (ADR-0015). Open questions: how a
timestamp becomes text, what an event that never happened prints, what a speed is divided
by, and what prints when there are no timings at all.

Measured on 2026-09-26 with the reference build (curl 8.21.0, mingw, Schannel) through
`Record-CurlExchange.ps1` against a loopback 200 with a 20000-byte body:
`curl -s -o NUL -d <5000 bytes> -w "..." http://127.0.0.1:18226/` printed
`ns=0.000065|c=0.005721|a=0.000000|pre=0.006038|post=0.006038|st=0.057008|r=0.000000|t=0.057123|sd=350170|su=87542`.
The same without `-d` to `localhost` printed `t=0.251240` and `sd=79607`, `su=0`.
`file:///` of 100000 bytes printed `sd=0`; a refused connect to `127.0.0.1:1` printed
`c=0.000000`, `sd=0` and `su=0`.

## Decision

- **Times are whole microseconds since `TransferTimings.Started`, printed as
  `<seconds>.<six digits>`**, as curl's `tool_writeout.c` prints a `curl_off_t` of
  microseconds. The elapsed time comes from `TimeProvider.GetElapsedTime` on the provider
  that took the timestamps, which `TransferWriteOutVariables` now takes as a constructor
  argument, and is truncated to microseconds.
- **An event that happened is at least one microsecond after the start**, as
  `Curl_pgrsTime` makes it. An event that did not happen, a transfer with no timings, and
  `time_redirect` with no redirect print `0.000000`.
- **Speeds are curl's `trspeed`: size times a million, divided by `time_total` in
  microseconds, truncated**; for a size too large to multiply, size divided by whole
  seconds, and `long.MaxValue` under one second. Without timings both speeds are `0`, as
  the measured `file://` and refused transfers print.

## Consequences

- Every `-w` time and speed is exact for a report whose timings are exact, and tests pin
  it with a clock whose timestamps are microseconds.
- curl divides by the time of its last progress update, a few microseconds before
  `time_total` (0.057115 against 0.057123 above), which is not reported. Our speeds can
  therefore differ from curl's in the last digits for the same transfer; the times cannot
  be compared byte for byte across runs anyway.
- curl prints a non-zero `time_namelookup` for a literal address and non-zero
  `time_pretransfer`, `time_posttransfer` and `time_starttransfer` after a refused
  connect (measured `pre=2.027121`); we print what the handler recorded, which is `null`
  and so `0.000000` in both cases. Matching that is a change to where handlers take the
  timestamps, not to the formatting.

## Alternatives considered

- **`TimeProvider.System` inside the formatter.** Wrong for any timestamp taken by another
  provider, such as the fakes in the tests, and against the rule to inject `TimeProvider`.
- **Printing seconds with `double` and `F6`.** Rounds where curl truncates, and depends on
  floating-point for values curl computes in integers.
- **Dividing a speed by `time_starttransfer` to `time_total`.** Not what curl does; curl
  divides by the time since the transfer began.
