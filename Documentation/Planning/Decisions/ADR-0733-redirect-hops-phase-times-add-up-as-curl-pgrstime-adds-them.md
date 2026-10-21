# ADR-0733 — A redirect chain's phase times add up hop by hop, as curl's `Curl_pgrsTime` adds them

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2959
- Decided by Claude under Stewart's delegation.

## Context

curl 8.22.0's `Curl_pgrsTime` adds to `t_nslookup`, `t_connect`, `t_appconnect`, `t_pretransfer`,
`t_posttransfer` and `t_starttransfer` the time from the current hop's start (`t_startsingle`) to the
event, and `TIMER_STARTSINGLE` starts each hop without clearing them. So after `-L`,
`%{time_namelookup}` and the rest are the sum of each hop's own phase time, without the time between
hops, which `%{time_redirect}` reports. Upstream `tests/unit/unit1399.c` pins it: a phase that took 2 s
on the first hop and 1 s on the next is 3 s.

Curl's `RedirectFollower` kept the last hop's timestamps and measured them from the first hop's start,
so every phase of the last hop also counted every earlier hop in full (4 s in unit1399's scenario).

## Decision

`Curl.Core`'s new `RedirectHopTimings` merges a chain's timings: each phase is the first hop's start
plus the sum, over the hops that reached it, of the time from that hop's start to the event. A phase
an earlier hop reached keeps its time even when the last hop did not, as curl's accumulator does; one
no hop reached stays `null` (printed as 0). `Started`, `Completed` and `RedirectDuration` are as
before, so `%{time_total}` and `%{time_redirect}` do not change. `RedirectFollower`'s chain uses it,
and `WriteOutTransferInfo` and `TransferTimings` are unchanged: the merge writes synthetic
timestamps that measure the sums from the first start.

curl also raises each hop's phase time to at least 1 µs before adding it; Curl raises only the total,
as it did, since a hop measured below 1 µs is not reachable on a real transfer.

## Consequences

- Upstream case 1399 runs as `Unit1399RedirectAdditiveTimers` over `RedirectHopTimings` and
  `WriteOutTransferInfo.FromReport`, and passes.
- `-w` phase times after a redirect are now smaller than `%{time_redirect}` plus the last hop's, as in
  curl.
