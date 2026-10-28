# ADR-0945: Standard output and standard error write at the handle's shared file offset

- Status: Accepted
- Date: 2026-10-28
- Decision: Decided by Claude under Stewart's delegation (BL-3937)
- Relates to: [ADR-0866](ADR-0866-off-windows-curl-never-touches-the-dotnet-terminfo-console.md) (standard streams opened over their descriptors), [ADR-0083](ADR-0083-thin-socket-adapters-are-measured-by-the-integration-run.md)

## Context

`StandardOutputOpener.OpenHandle`, which opens a redirected standard output and, off Windows, standard
error, wrapped the handle in an unbuffered `FileStream`. Over a seekable file a `FileStream` keeps a
position of its own, read once when it is made, and writes there (`pwrite` on Linux and macOS,
`WriteFile` with an `OVERLAPPED` offset on Windows). So when the shell shares one file between itself,
standard output and standard error (`{ echo before; curl ...; echo after; } > f 2>&1`), Curl's two
streams overwrite each other and the shell's next write lands at an offset Curl never moved. curl writes
through C `stdout` and `stderr`, whose `write(2)` (`WriteFile` without an offset on Windows) uses and moves
the descriptor's shared offset. Measured on 2026-10-28 in WSL (Ubuntu) against curl 8.18.0:

| Command | curl 8.18.0 | Curl before | Curl after |
| --- | --- | --- | --- |
| `{ echo before; curl -sS http://127.0.0.1:21199/; echo after; } > /tmp/w 2>&1` | `before`, the `curl: (7)` line, `after` | the `curl: (7)` line after `after`, its start overwritten (BL-3824) | `before`, the `curl: (7)` line, `after` |

## Decision

- `OpenHandle` wraps the handle in `SharedOffsetHandleStream`, an unbuffered write-only stream that hands
  each write to a shared-offset write until every byte is written; a write that moves no byte throws an
  `IOException`, as a failed one does, so exit 23 and `WriteFailureIgnoringStream` work as before.
- The write is `NativeSharedOffsetWrite`: `WriteFile` with no `OVERLAPPED` on Windows, and elsewhere the
  runtime's `SystemNative_Write` shim in `libSystem.Native` (a fixed signature, retrying on `EINTR`), the
  shim ADR-0869 and ADR-0943 already call. Its error number becomes the `IOException`'s.
- Windows gets the same stream: curl's Windows build writes through the C runtime's `WriteFile` at the
  shared file pointer too, and two `FileStream`s over one `> f 2>&1` handle overwrote each other there as
  well (`StandardOutputOpenerTests.OpenHandle_TwoStreamsOverOneFile_WritesEachAfterTheOther`).
- The P/Invoke adapter is excluded from coverage (ADR-0083); the stream's logic is unit tested with a fake
  write, and the adapter runs under `StandardOutputOpenerTests` on every platform.

## Consequences

A file shared with the shell or between standard output and standard error gets Curl's bytes where
curl's go. Pipes, terminals and devices behave as before. The stream is not seekable, which no caller of
standard output relied on.
