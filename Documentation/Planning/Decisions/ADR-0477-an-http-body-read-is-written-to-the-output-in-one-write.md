# ADR-0477: An HTTP/1.x body read is written to the output in one write, and reported in curl's 16 KiB client writes

- Status: Accepted
- Date: 2026-10-11
- Task: BL-2043 (re-fix of audit finding AF-0144)
- Decided by Claude under Stewart's delegation

## Context

AF-0144: a 50 MiB `-o` download took 1.47 times curl's median wall time, and still 1.39 times
after BL-1963 made each read take curl's 100 KiB receive buffer, because `HttpResponseBodyReader`
still wrote every read to the output in 16 KiB pieces. Every `-o` file opens with a synchronous
handle (`PhysicalFileSystem`), so each `Stream.WriteAsync` is queued to the thread pool; measured
in-process, writing 50 MiB to such a `FileStream` took 31 ms in 16 KiB writes and 21 ms in
100 KiB writes, and the hops cost more when other processes load the CPU.

Curl 8.21.0 still shows 16 KiB client writes: `-v` prints a `{ [16384 bytes data]` line per
piece, and a failed write reports `passed 16384` (measured, BL-170).

## Decision

For a Content-Length or read-to-close body that is not decoded, each read goes to the output as
one write. After it, the read is reported in pieces of at most 16384 bytes, each a data event
followed by the downloaded count, so `-v`, the trace and the progress meter see what they saw
before. When the write fails, the output's `OutputWriteFailedException.BytesAccepted` places the
failure in its 16 KiB piece: the message gives that piece's `passed` and `returned`, the whole
pieces before it count as written, and only the pieces up to it are reported. A failure that
says nothing of what was accepted (a bare `IOException`) is placed in the first piece.

A decoded body (`--compressed`, `--tr-encoding`) keeps writing in 16 KiB pieces, because its
output accepts decoded bytes, which cannot place a failure in an encoded piece. The body prefix
and chunked data keep their own writes.

## Consequences

A large download makes a sixth of the output writes. `--trace -` with the body on standard output
now shows a read's data events after the read's body bytes rather than between its pieces, and
an output failing mid-read without saying how much it accepted reports the read's first piece.
