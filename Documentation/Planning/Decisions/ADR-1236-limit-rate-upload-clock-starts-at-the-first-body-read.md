# ADR-1236 — The `--limit-rate` upload clock starts at the first body read

- Status: Accepted
- Date: 2026-11-05
- Task: BL-4875
- Decided by Claude under Stewart's delegation.

## Context

Finding AF-0684: `--form a=b --limit-rate 1` against `Record-CurlExchange.ps1` (which stops reading
after a second without a byte) usually recorded one body byte from curl 8.22.0 (Schannel) and two to
four from Curl. `--trace-ascii --trace-time` showed why. curl sent the head and the first body byte
together and the second byte 1.2 s later. Curl sent the second byte 0.28 s after the first, because
`RateLimitedUploadStream` (ADR-0745) built its `RateLimitedStream` when the transfer was set up. The
limiter's clock therefore ran while Curl connected and sent the head, and the first wait was mostly
used up by then. After that, both binaries pace on a schedule counted from their start, catching up
after a late byte (curl's gaps were 1.2 s, 0.81 s, 1.0 s).

## Decision

`RateLimitedUploadStream` builds its `RateLimitedStream` at its first asynchronous read, so the
rate's clock starts with the first body piece. That piece goes at once and each later one follows on
the same cumulative schedule as before. `RateLimitedStream` itself is unchanged, so the download
limit's pacing is unchanged too.

## Consequences

The second body byte now goes a full second after the first, as curl's does. Over six runs on
fresh ports, curl recorded 2,1,1,1,1,1 body bytes and Curl 2,5,1,1,1,2, where before the change it
recorded 3,4,4,3,2,2. Some runs still differ because the recorder's one-second read window sits right
on the pacing interval, and that timing jitter affects curl too (it recorded 6 bytes in one earlier run).
