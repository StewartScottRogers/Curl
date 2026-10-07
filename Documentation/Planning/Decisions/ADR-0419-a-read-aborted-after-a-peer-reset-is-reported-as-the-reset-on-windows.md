# ADR-0419 — A read aborted after a peer's reset is reported as the reset on Windows

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1450
- Decided by Claude under Stewart's delegation.

## Context

A server that resets the connection right after accepting it made Curl print
`curl: (56) Recv failure: Connection was aborted` on Windows, where curl 8.21.0's Schannel build
prints `curl: (56) Recv failure: Connection was reset`, for every protocol reading through
`StreamConnection` (`http`, `dict`, `gopher`, `mqtt`, `ws`).

Measured on 2026-10-07 with `Record-CurlExchange.ps1 -Reset` and a .NET client run as the "curl":
the request's send succeeds and the RST arrives after it. The first receive is the first call to
see the reset, and Windows answers it with WSAECONNABORTED (10053) when it is issued more than a
few milliseconds after the RST arrived, and with WSAECONNRESET (10054) when it is issued sooner
or is already waiting. Overlapped, blocking, non-blocking, after-`select` and zero-byte receives
all behave alike, and `SO_ERROR` reads 0, so no socket call recovers the reset. Delaying the read
30 ms gave 10053 in 8 of 8 runs; real curl's `recv` runs inside the window and printed the reset
in 9 of 10 interleaved runs (and the abort in 5 of 8 back-to-back runs, so curl is subject to the
same race). Curl's first read after the request comes later, and printed the abort every time.

## Decision

On Windows `StreamConnection.ReadAsync` reports a read failing with
`SocketError.ConnectionAborted` as an `IOException` carrying a `SocketException` with
`SocketError.ConnectionReset` and the same message (`ReportsAbortedReadAsReset`, an internal init
property defaulting to `OperatingSystem.IsWindows()`, so tests pin both settings on any platform).
Writes, and every other platform, are unchanged.

## Consequences

- The five measured URLs print curl's `Recv failure: Connection was reset` (15 of 15 runs).
- A read aborted by Windows' own stack for another reason (a retransmission time-out) is worded
  as a reset where curl would say aborted. Those need a peer that stops answering mid-transfer,
  are rare, and are timing-dependent in curl too; the common case, a peer's RST, now matches.
- Matching curl by reading sooner instead (a read posted before the request is written) was
  rejected: it changes every protocol's read order, and a read posted after an early RST
  returned 0 bytes in the measurement, which would turn exit 56 into exit 52.
