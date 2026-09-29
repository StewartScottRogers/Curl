# ADR-0153 — Parallel transfers to one http host wait for the first to end unless --parallel-immediate

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-520.

## Context

Under `-Z`, curl sets `CURLMOPT_MAX_HOST_CONNECTIONS` from `--parallel-max-host` and, unless
`--parallel-immediate` is given, `CURLOPT_PIPEWAIT` on every transfer: a transfer that finds a
connection to its host still being set up waits to see whether it can multiplex on it. Curl speaks
HTTP/1.1 only (ADR-0017) and has no connection pool shared across a `-Z` run's transfers, so what
matters is when each transfer starts talking to its host.

curl 8.21.0 (Schannel, Windows 11, the reference build of ADR-0009 and ADR-0018) was measured on
2026-09-28 with `Record-CurlExchange.ps1 -Connections 3 -ResponseDelayMilliseconds 500`, whose
server answers one connection at a time. The URLs were `http://127.0.0.1:P/a`, `/b` and `/c`, with
`-w '%{urlnum} nc=%{num_connects} cid=%{conn_id} tc=%{time_connect} tt=%{time_total}'`.
`time_connect` counts from the transfer's own start, which for a transfer that did not wait for a
slot is the run's start.

| Options | Result |
| --- | --- |
| none | `/a` connects at once; `-v` shows `/b` and `/c` "Waiting on connection to negotiate possible multiplexing" and connecting only once `/a` has ended (0.53 s); three connections |
| `--parallel-immediate` | all three connect at once (`tc` under 1 ms each); three connections |
| `--parallel-max-host 1` | one at a time: `tc` 0.00, 0.52 and 1.03 s |
| `--parallel-max-host 1 --parallel-immediate` | the same |
| none, response held open 1 s after a short body (exit 18) | `/b` and `/c` wait until `/a` has ended (1.36 s), not until its headers arrive (0.3 s) |
| `--parallel-max 2 --parallel-max-host 1 --parallel-immediate`, `/c` on `localhost` | `/c` starts only once `/a` has ended: `/b`, waiting for its host, keeps the second slot |

Each transfer made one connection (`num_connects` 1) and connection ids ran 0, 1, 2 in every case.
With a URL on another host in the run, the pipe-wait sometimes ended early, woken by that host's
connection events. This is an artefact of curl's event loop and was not reproducible on purpose.

## Decision

1. `ParallelHostQueue` (in `Curl.Console`, owned by the run's `ParallelRun`) holds a transfer back
   after it has taken its `--parallel-max` slot, so a waiting transfer keeps its slot, as measured.
2. A host is the URL's `host:port`, as `System.Uri` gives it (lower-case host, the scheme's default
   port). A URL with no host, such as `file://`, is never held back.
3. At most `--parallel-max-host` transfers run to one host (zero means no limit), across schemes.
   Waiting transfers start in the order they began to wait.
4. Without `--parallel-immediate`, an `http://` transfer waits while another transfer to its host is
   running and none to that host has ended yet, so the first runs alone and the rest start together
   when it ends. Only `http://` waits. curl ends the wait at TLS negotiation (ALPN) for `https://`,
   which Curl cannot observe from the runner and which takes no measurable time next to a transfer.
   Other schemes never multiplex.
5. The early wake-up caused by another host's connection events is not reproduced.
6. A redirect (`-L`) to another host counts against the host of the transfer's own URL.

## Consequences

- A `-Z` run to a single `http://` host without `--parallel-immediate` makes its first request
  alone, as curl's does, so the timing matches curl's.
- The rule is deterministic and unit-testable with held transfers. Where curl is racy, Curl is not.
- `--parallel-max-host` does not follow redirects. Following them would need the handler to report
  each hop's host to the runner.

## Alternatives considered

- **Ignore `--parallel-immediate`.** Every transfer to one host would start at once. It was
  rejected because it differs from the measured connection timing without the option.
- **Wait for the first transfer's response headers.** It was rejected because the measurement shows
  that the wait lasts until the transfer ends.
- **Hold a waiting transfer outside its `--parallel-max` slot.** It was rejected because curl keeps
  the slot, as the last measured row shows.
