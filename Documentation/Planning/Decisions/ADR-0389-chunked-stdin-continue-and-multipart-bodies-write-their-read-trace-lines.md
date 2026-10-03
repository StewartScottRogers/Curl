# ADR-0389: Chunked, stdin, 100-continue and multipart bodies write their [READ] trace lines

- Status: Accepted
- Date: 2026-10-02
- Task: BL-1214
- Decided by Claude under Stewart's delegation.

## Context

ADR-0383 (BL-1189) traced only HTTP/1.x `-d` and known-length `-T` bodies sent unchunked without
waiting for `100 Continue`. curl 8.21.0 (Schannel build) was measured with `Record-CurlExchange.ps1`
on 2026-10-02 for `-T -`, `-T -` with `-H Expect:`, `-T` of a 1 100 000-byte file (both a
`100 Continue` and a timed-out wait), `-H 'Transfer-Encoding: chunked' -d ab`, `-F a=b` and `-F` of a
70 000-byte file (BL-1214 Notes).

## Decision

`HttpRequestBodyWriter` traces every non-empty HTTP/1.x body under `TracesReaders`:

1. A `-T` upload of unknown length (stdin) adds `add fread reader, len=-1`; its `cr_in_read` lines
   write `total=-1`, and the empty read that ends it is reported with `eos=1`.
2. A multipart (`-F`) body adds no reader line; each read writes
   `cr_mime_read(len=N), mime_read() -> R` and `cr_mime_read(len=N, total=T, read=S) -> 0, R, E`, where
   `N` is the room or the rest of the body, whichever is smaller.
3. A chunked body writes `http_chunk, made chunk of R bytes -> 0` after each read that gave bytes and
   `http_chunk, added last, empty chunk` with the read that reached the end; its `client_read` counts
   the 12 bytes of framing room and the bytes with their framing (and the closing chunk's 5).
4. A body that waits for `100 Continue` writes its reader line and `client_read(len=65536-head) -> 0,
   nread=0, eos=0` before the head, and `client_read(len=65536) -> 0, nread=0, eos=0` after it,
   whether the wait then ends with `100 Continue` or runs out (`WriteHeadBeforeContinueAsync`).

HTTP/2 and HTTP/3 bodies still write none: they were not measured.

## Consequences

`-T -`, `-T` over 1 MiB, chunked `-d` and `-F` match curl's `[READ]` lines byte for byte. A request
whose wait is ended by a final status writes the reader and held-back reads and nothing more, which
was not measured. HTTP/2 and HTTP/3 bodies and redirect rewinds remain follow-ups.

## Alternatives considered

- Leaving `100 Continue` bodies untraced: loses the most common `-T` shape over 1 MiB and stdin.
- Tracing multipart bodies with an `add ... reader` line like the others: curl writes none.
