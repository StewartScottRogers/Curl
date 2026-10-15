# ADR-0493: CURL_TIME fixes the HSTS clock only behind the harness's seam

- Status: Accepted
- Date: 2026-10-14
- Task: BL-2234
- Decided by Claude under Stewart's delegation

## Context

Upstream cases 446 and 780 to 783 send HSTS headers through the http-proxy and verify the `--hsts`
file curl writes back, whose expiry dates count from a clock the case fixes with `CURL_TIME`. Only
curl's debug build reads that variable (`lib/hsts.c`'s `hsts_debugtime`, in place of `time()` for
the HSTS cache alone), so the cases name the `Debug` feature and the harness skipped them. A release
build ignores the variable.

## Decision

The dialing `CurlComposition.CreateRunner` takes `honoursHstsTimeVariable` (off by default). When it
is on and `CURL_TIME` holds a whole number of seconds since the epoch (at most 9999-12-31T23:59:59Z),
`CurlComposition.HstsClockOf` makes a `FixedWallClock` standing at that instant and the runner gets it
as `hstsClock`: the run's `HstsTransferPolicy` counts expiries on it, and the transfer contexts' HSTS
store is a `ClockOverridingHstsStore`, so a `Strict-Transport-Security` header's `max-age` counts from
it rather than from the transfer's clock. Nothing else in the run reads it. A value that is not such a
number leaves the run's own clock.

The conformance tests turn the seam on for every case and report `Debug` to a case that names the
`HSTS` feature and sets `CURL_TIME`, as they already report it to the httpsig cases (BL-2183). The
shipped executable never turns the seam on, so `curl` ignores `CURL_TIME` as curl's release build
does.

## Consequences

- 780, 781, 782 and 783 pass and join the ratchet.
- 446 runs and fails: it also needs `CURL_HSTS_HTTP`, the debug build's switch that learns HSTS over
  plain `http` (BL-2250).
- Other `CURL_TIME` cases (970, 972, 1981) still skip for `Debug`; they fix other clocks.
