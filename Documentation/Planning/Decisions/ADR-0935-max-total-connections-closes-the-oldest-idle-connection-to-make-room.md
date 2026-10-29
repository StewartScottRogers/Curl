# ADR-0935: CURLMOPT_MAX_TOTAL_CONNECTIONS closes the oldest idle connection to make room

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3679 (CGR-150)
- Decided by Claude under Stewart's delegation.

## Context

Under `CURLMOPT_MAX_TOTAL_CONNECTIONS` the engine's multi handle (`ManagedMultiTransfer.CanStart`, ADR-0605)
counted only the connections its running transfers hold. A finished transfer's connection stayed idle in the
multi handle's pool, so a transfer to another host opened a second connection while the first was still open:
two connections under a limit of 1. The third-party gap report (CGR-150) measured libcurl 8.22.0 closing the
first connection before the second host's request arrives, and four handles alternating two hosts each opening
a connection of its own.

libcurl 8.22.0 first looks for a connection to reuse; only when none fits does `Curl_cpool_check_limits` run,
which, while the pool holds `CURLMOPT_MAX_TOTAL_CONNECTIONS` connections or more, closes the oldest idle one,
and holds the transfer pending only when no idle connection is left to close.

## Decision

Before a transfer starts under a non-zero `MaxTotalConnections`, `ManagedMultiTransfer` calls
`CloseIdleConnectionsToMakeRoom` (`ManagedCurlEngine.CreateMulti` sets it to the multi pool's
`ConnectionCache.CloseIdleToMakeRoomAsync`) with the limit and the URL's host and port, and awaits it before
the perform. Unless an idle connection to that host and port (host ignoring case) waits to be reused, the
cache closes its oldest idle connections until fewer than the limit, idle and in use, remain. A connection in
use is never closed, and `CanStart` keeps counting running transfers only, so a transfer is held pending only
by connections in use, as libcurl's is.

The reuse check compares host and port only, not the whole pool key (scheme, TLS, proxy): the closest match the
multi handle can make from the URL before the transfer's pool key exists. A transfer that will not reuse the
idle connection to its host keeps it, opening one more; that edge is left as is.

## Consequences

- `probe C_66` (A then B, limit 1): no connection to A is open when B's request arrives.
- `probe C_09` (four handles alternating two hosts, limit 1): four connections, never two open at once.
- Two transfers to one host under the limit still reuse one connection.
- Tests: `Curl.Engine.UnitTests/Cgr150Tests.cs`.
