# ADR-0355 — `--tcp-fastopen` on macOS connects through `connectx`

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1101.

## Context

ADR-0317 sets the raw `TCP_FASTOPEN` (`0x105`) option on macOS for `--tcp-fastopen`, the step
.NET's `Socket` allows. Client Fast Open proper on Darwin is `connectx` with
`CONNECT_DATA_IDEMPOTENT | CONNECT_RESUME_ON_READ_WRITE`, which libcurl 8.21.0's `cf-socket.c`
calls there so the first request rides in the SYN. The BCL does not wrap `connectx`, and a
managed `Socket` learns it is connected only from its own connect, or from the peer name it
reads when it is made from a handle.

## Decision

1. `FastOpenSocketOption.ConnectsThroughConnectx` decides the route: `connectx` only with
   `--tcp-fastopen` on Darwin. It is unit tested on every platform.
2. `DarwinFastOpenConnect.TryConnect` calls `connectx` through `[LibraryImport("libc")]` (AOT
   safe; the library now allows unsafe code for the generated marshalling), with no source
   address and the destination's native `sockaddr`, then wraps a `dup` of the descriptor in a
   new `Socket`, which reads the peer name and so knows it is connected, and disposes the
   original. It is excluded from coverage per ADR-0083 and pinned by a macOS-only loopback test.
3. When `connectx` refuses, the dialer connects as usual with `ConnectAsync`, so a platform
   without the call, or an error, still reports through .NET's own exceptions. libcurl does
   not fall back, but there an error from `connectx` is a connect failure, which a second
   connect reports the same way.
4. The raw `TCP_FASTOPEN` option of ADR-0317 is still set on macOS. libcurl does not set it on
   a client socket, but it is harmless there, and keeping it leaves ADR-0317's tests as they are.
5. No output changes: libcurl's `-v` says nothing of the route. curl 8.21.0 on macOS was not
   measured, as the lane runs on Windows; nothing is pinned that would need it.

## Consequences

`--tcp-fastopen` on macOS sends the first request with the SYN to a server that accepts Fast
Open, as curl does. A connection that is refused surfaces at the first write or read rather than
at the connect, as it does in curl, since the SYN leaves only then.
