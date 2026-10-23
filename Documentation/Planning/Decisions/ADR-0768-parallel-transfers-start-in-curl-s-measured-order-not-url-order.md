# ADR-0768 — -Z transfers to one host start in curl 8.22.0's measured order, not URL order

- Status: Accepted
- Date: 2026-10-22
- Task: BL-3178
- Decided by Claude under Stewart's delegation.

## Context

BL-3178 was filed on the premise that curl starts `-Z` transfers in URL order, after Stewart saw
Curl send `/echo`, `/utf8`, `/default` for `curl -Z http://h/echo http://h/default http://h/utf8`.
Measured on 2026-10-22 with `Record-CurlExchange.ps1` (a loopback server that answers each
connection at once with an empty 200 and closes it; `-s -w '%{url} '`; four URLs `/echo`,
`/default`, `/utf8`, `/four` to one host; three runs each):

| `--parallel-max` | curl 8.22.0 request order = stdout order (every run) | Curl at `02ec93908` (three runs) |
| --- | --- | --- |
| 50 (default) | `/echo /utf8 /four /default` (three URLs: `/echo /utf8 /default`) | `/echo /utf8 /default /four`, `/echo /default /utf8 /four`, `/echo /default /four /utf8` |
| 2 | `/echo /utf8 /default /four` | `/echo /default /utf8 /four` every run |
| 1 | `/echo /default /utf8 /four` | `/echo /default /utf8 /four` every run |

So curl does not start them in URL order. What Stewart saw from Curl is what curl itself does for
three URLs; Curl's order is the one that is wrong only in being a race: `ParallelHostQueue.Leave`
releases every waiter of the host at once, each continuation runs on the thread pool, and the
connects race.

curl's order fits one rule: the first transfer to wait on the host (the one that found the host's
first connection still in progress, curl's "pending" transfer) is put back behind every transfer
that starts in the same step when the host's first transfer ends; the rest keep URL order. At 50:
`/default` waited first, `/utf8` and `/four` start ahead of it. At 2: `/echo` and `/default` hold
the two slots; when `/echo` ends its slot goes to `/utf8`, which starts ahead of `/default`, and
`/four` waits for a slot. At 1 nothing waits on the host.

## Decision

1. Curl matches curl 8.22.0's measured order, not URL order: the platform's curl is the standard
   (CLAUDE.md, "Decisions"), and a script that compares output with curl's must see the same order.
2. The order is made deterministic: transfers released together start one after another in that
   order, each having sent its request before the next begins, rather than racing on the thread pool.
3. The work is BL-3181's (filed by BL-3178, which ran out of time after measuring); `-Z` with
   `--parallel-max 1` already matches and stays as it is.

## Consequences

- BL-3178's criteria asking for URL order are replaced by ones pinning the measured order.
- A keep-alive server was not measured: the recorder's `-Script` mode hung for 30 minutes on the
  attempt (BL-3178 Notes). BL-3181 measures it first; if curl reuses the first connection for one
  waiter, the rule is extended to say which.
