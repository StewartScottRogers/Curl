# ADR-0525: A chunked upload of unknown length sends its last data chunk and the closing chunk in one write

- Status: Accepted
- Date: 2026-10-15
- Task: BL-2341 (gap finding GF-0082, upstream test60)
- Decided by Claude under Stewart's delegation.

## Context

Upstream test60 uploads `more than one byte` from stdin with `-T - -H 'Content-Length: 1'`. The
request is chunked; the test server reads the one byte its `Content-Length` promises, answers and
closes. The reference curl's request ends with the closing `0\r\n\r\n` chunk and it exits 0. On
Linux the gap harness recorded Curl's request stopping at byte 179, exactly where the closing
chunk starts, and on macOS Curl exited 56 writing that chunk to a closed connection.

`HttpRequestBodyWriter` wrote each piece it read from the stream as soon as it read it: the
held head with the first data chunk in one write, then, after a second read of stdin returned
nothing, `0\r\n\r\n` in a write of its own. Between the two writes the server could read the
request, answer and close, so the closing chunk met a reset connection (Linux, where the
recorded bytes stop at the first write) or a failed send (macOS, exit 56). Whether it does is a
race, which is why a pipe into `nc` in WSL did not show it.

Curl 8.21.0 fills its upload buffer from the client reader until the buffer is full or the reader
reports the end of the stream, and only then sends it: the read that gives the data and the read
that gives nothing, which makes the chunk encoder add the closing chunk, both happen before the
send, so a short body goes out with its closing chunk in one send.

## Decision

For a chunked stream body of unknown length (`-T -`, a multipart body of unknown length) sent
without waiting for `100 Continue`, the writer holds back the bytes of each piece until the next
read: a read that gives more releases them in one write, and a read that gives nothing leaves
them to go out with `0\r\n\r\n` in the same write. A read that fails the transfer (exit 26)
releases them first, so the chunks read before the failure still reach the server as before.

The per-read `--trace-config read` lines, the progress meter and the data events are unchanged;
only where the socket writes fall moves. Bodies of known length, bodies of bytes and bodies sent
after `100 Continue` (whose early-response watch sends piece by piece) are unchanged.

## Consequences

- A request whose server answers on its first data closes no longer loses the closing chunk.
- A chunk read from an interactive stdin is sent when the next read returns, not at once; curl's
  own `fread` of stdin already waits for its buffer to fill or the stream to end, so this does not
  hold back anything curl would have sent.
- The gap measurement of `behaviour:test60` on Linux and macOS is checked in a gap-platforms run
  by an interactive session (BL-2342), since lanes cannot run the gap office's tools.
