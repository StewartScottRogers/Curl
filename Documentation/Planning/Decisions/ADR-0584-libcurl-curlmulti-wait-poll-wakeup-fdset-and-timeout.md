# ADR-0584 — `Libcurl`'s `CurlMulti` waits through the engine, refuses a negative timeout itself, and answers `curl_multi_fdset` from the engine's wait sockets

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2430.

## Context

Phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md` lists `curl_multi_wait`, `_poll`,
`_wakeup`, `_fdset` and `_timeout` for `CurlMulti`. The seam's `IMultiTransfer` has `WaitAsync`,
`PollAsync`, `Wakeup`, `GetTimeout` and `GetWaitSockets` (`curl_multi_waitfds`), and no `fdset`
member: its doc comment leaves to phase 3 how `Libcurl` answers it. The managed engine has no multi
transfer yet; that is engine-side work.

## Decision

1. `Libcurl/Multi/Wait` adds a `CurlMulti` part: `WaitAsync`, `PollAsync`, `Wakeup`, `Timeout` and
   `FdSet`. The waits are async only, beside `PerformAsync`: `CurlEasy.Perform` stays the one
   sync-over-async bridge in `Libcurl`.
2. The waits take libcurl's `int timeout_ms`. A negative one answers `CURLM_BAD_FUNCTION_ARGUMENT`
   and 0 sockets without calling the engine, as `multi.c`'s `multi_wait` does; any other is handed to
   the engine as a `TimeSpan`, with the extra sockets, whose `ReturnedEvents` the engine sets.
3. `Wakeup` and `Timeout` pass the engine's answers through.
4. `FdSet(read, write, exception, out maxSocket)` reads the engine's wait sockets
   (`GetWaitSockets` once with no room to learn the count, then once with room) and adds each socket
   waiting for `PollIn` to `read`, `PollOut` to `write` and `PollPriority` to `exception`, which
   libcurl's own sockets never wait for. A set is any `ISet<CurlSocket>`, the .NET `fd_set`; like
   `FD_SET`, sockets are only added, and a `null` set is skipped. `maxSocket` is the highest socket
   added, or -1 when there is none or the engine fails; on a failure no set changes. There is no
   `FD_SETSIZE` limit to skip sockets past, since an `ISet` has none.
5. Every member of a cleaned-up handle throws `ObjectDisposedException`, as `Multi/Core`'s do.

## Consequences

- `fdset` needs no seam member, and stays true to whatever `waitfds` an engine reports.
- An engine whose socket count grows between the two reads answers `OutOfMemory`, passed through;
  the caller calls again, as for `waitfds` itself.
