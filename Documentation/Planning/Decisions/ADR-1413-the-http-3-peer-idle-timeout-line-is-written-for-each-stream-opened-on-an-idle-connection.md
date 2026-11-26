# ADR-1413 — The HTTP/3 peer idle timeout line is written for each stream opened on an idle connection

- **Status:** Accepted
- **Date:** 2026-11-25

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5995.
Replaces ADR-0388's "as the connection's first request stream opens".

## Context

curl 8.18.0's ngtcp2 build, measured with `Record-CurlExchange.ps1 -Http3StalledOrigin` and
`--http3-only -k -v --trace-config http/3 -m 2 <url>/x <url>/y <url>/z`, writes
`[HTTP/3] peer idle timeout is <n>ms, set keep-alive to <n/2> ms.` before `OPENED stream` for:

- streams 0, 4 and 8 when the transfers run one after another (BL-5982);
- stream 0 only with `-Z`, where `/y` and `/z` reuse the connection while the earlier
  streams are still active.

So curl sets the keep-alive, and says so, when a stream opens on a connection with no
active stream.

## Decision

`Http3Session` counts the request streams being opened or carrying a transfer not yet marked
done (`EndRequestStream`). A stream that raises the count from 0 to 1 has
`Http3StreamTrace.OpeningStreamOnIdleConnection` write the line before it opens. A stream
that fails to open leaves the count.

## Consequences

- A timed-out transfer is marked done (ADR-1410), so the next URL writes the line again, as curl does.
- A transfer that fails otherwise is not marked done (ADR-0388), so the connection still
  counts as busy and the next stream writes no line; BL-5996 covers it.

## Alternatives considered

- **Counting in `Http3ClientSession`:** it already counts streams in use, but reports the count
  only when a stream ends, and the extensions that share it write no curl trace lines.
