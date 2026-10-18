# ADR-0690: The upstream case runner lists the `wakeup` feature

- Status: Accepted
- Date: 2026-10-18
- Decided by Claude under Stewart's delegation.

## Context

`UpstreamCurlPlatform` kept `wakeup` off as a libcurl API feature a command-line tool does not have,
so upstream cases 1564 and 1565 (found by BL-2542) were skipped with `Curl lacks the feature wakeup`.
Since then the libtest programs run over `Libcurl`, which has `curl_multi_wakeup` (ADR-0584, ADR-0597).
`form-api` and `headers-api` were turned on for the same reason (BL-2593, BL-2264).

## Decision

- `UpstreamCurlPlatform` lists `wakeup` on every platform. Five vendored cases need it: 1564, 1565,
  2405, 2412 and 2414.
- Measured through the runner on Windows: 1564, 1565, 2412 and 2414 pass and join
  `PassingUpstreamCases.txt`. 2405 fails (`expected exit code 0, got 44`); it stays off the list and
  BL-2743 files the fix.
- A unit test pins that every platform lists `wakeup`.

## Consequences

Four more upstream cases hold the ratchet. `wakeup` was the only libcurl API feature listed here
whose function `Libcurl` has; `Debug`, `TrackMemory`, `unittest`, `shuffle-dns`, `override-dns`,
`threadsafe` and `getrlimit` stay off.
