# ADR-0546: A chunked upload of unknown length sent after the wait for 100 Continue also sends its last data chunk and the closing chunk in one write

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2371 (gap finding GF-0095, upstream test60 on macOS)
- Extends: ADR-0525
- Decided by Claude under Stewart's delegation.

## Context

ADR-0525 made a chunked stream body of unknown length (`-T -`) hold each chunk until the next
read, so its last data chunk and `0\r\n\r\n` go out in one write. It covered only bodies sent
without waiting for `100 Continue`. Upstream test60 is not one of them: curl adds
`Expect: 100-continue` to a `-T -` upload, the one-second wait runs out, and the body is then sent
through `HttpContinueWaitConnection.SendUnlessStoppedAsync`, piece by piece, so `0\r\n\r\n` still
went out in a write of its own. The test server reads the one byte its `Content-Length` promises,
answers and closes; on macOS the late closing chunk meets the closed socket, the connection is
reset, and the response read fails with exit 56 where curl exits 0 (GF-0095).

## Decision

A chunked stream body of unknown length sent after the wait for `100 Continue` holds each piece
back until the next read, as ADR-0525's bodies do: a read that gives more sends the held chunk
through `SendUnlessStoppedAsync`, and a read that gives nothing sends the held chunk and
`0\r\n\r\n` in one `SendUnlessStoppedAsync` call. A status of 300 or above still stops the send
(`CutShort`); the held piece, and a piece read after it, are then left unsent for a resend. A read
that fails the transfer (exit 26) sends the held chunk first.

The held chunk's data event (`--trace`) is reported when it is held, where curl 8.21.0's trace shows
it, before the next read (measured, BL-1214 Notes); `BytesSent`, `BytesWritten` and the progress
meter count it only once it is sent, so the `abort upload` and `close instead of sending` lines
after a stop stay true. The `--trace-config read` totals count the held chunk as read.

## Consequences

- `-T -` after an expired `100 Continue` wait no longer sends the closing chunk after a server that
  answered on the data has closed, so test60 behaves on macOS as it does on Windows and Linux.
- A 417 or other stop that arrives while a chunk is held stops the body one chunk earlier on the
  wire than before, which a resend makes up from the unsent bytes.
- The gap closes only when a gap-platforms run re-measures `behaviour:test60` on macOS as `match`.
