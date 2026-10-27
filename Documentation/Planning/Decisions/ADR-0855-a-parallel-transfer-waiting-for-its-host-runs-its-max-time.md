# ADR-0855 — A parallel transfer waiting for its host runs its max time

- **Status:** Accepted
- **Date:** 2026-10-26

Decided by Claude under Stewart's delegation (task BL-3638, CGR-109).

## Context

Under `-Z` without `--parallel-immediate`, `ParallelHostQueue` holds a second `http://` transfer to a
host until the host's first transfer ends (BL-520), as curl waits for the first connection to show
whether it can multiplex (`CURLOPT_PIPEWAIT`). CGR-109 (`Documentation/Reviews/Curl-Gap-Report-2026-10-25.md`)
found that the waiting transfer's `-m` did not run while it waited: the runner started the
`MaxTimeWatchdog` only once the wait ended, so when the first transfer timed out against a server that
never answers, the second connected and took its response. curl 8.18.0 (BL-3638 Notes) runs every
transfer's `-m` from the moment it is added to the multi handle: `curl -sS -Z -m 2 -w '[%{num_connects}]'
$H/stall $H/ok` opens one connection, prints `[1][0]` and two `(28) Operation timed out after N
milliseconds with 0 bytes received` lines, and exits 28.

## Decision

1. `CurlCommandRunner.WaitForHostWithinMaxTimeAsync` waits for the host under a positive `-m`: a wait
   that `-m` ends withdraws the transfer from the host's queue, and the transfer ends with exit 28 and
   `Operation timed out after N milliseconds with 0 bytes received`, having opened no connection.
2. The `-m` counts from when the transfer was added. curl adds every transfer of a batch in one loop,
   so `ParallelTransferQueue.BatchAddedTimestamp` is taken when the run starts its first transfer, and
   again when a slot comes free after a `--parallel-max` wait, and N is the time since then.
3. The wait's own timer runs a whole `-m` from when the wait began, which is after the host's first
   transfer started its `-m`, so that one ends first and is reported first (`[1][0]`), as in curl. A
   transfer the host frees once `-m` has passed since its batch was added leaves the host and times
   out instead of connecting.
4. A transfer freed before its `-m` has passed starts its attempt with a whole `-m`, as before. curl
   would give it only what is left; CGR-109 does not exercise that, and it is left as it is.

## Consequences

The CGR-109 repro gives curl's exit code, standard output, error lines and single connection.
`--parallel-immediate`, and a transfer that speaks HTTP/2 from the start, never wait for the host, so
they are unchanged. `Cgr109Tests` pins the cases with a clock whose timers fire only when the test
moves it.
