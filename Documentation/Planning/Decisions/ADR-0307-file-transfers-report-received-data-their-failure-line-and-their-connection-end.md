# ADR-0307 — `file://` transfers report received data, their failure line and their connection end

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-936.

## Context

Before BL-936, `FileProtocolHandler` reported one event through `context.Events`: the header-write
failure line. curl 8.21.0 (mingw, Schannel) was measured with `-v`, `--trace` and `--trace-ascii`
for a `file://` download, a `-T` upload, `-I`, a missing file and the failure cases listed in
BL-936's Notes. It writes `<= Recv data` blocks for a download's bytes (none for an upload or `-I`),
the failure's own message as an info line, and `* shutting down connection #N` or
`* closing connection #N` once a transfer got past its open.

## Decision

1. **A download reports each chunk it reads as `ReportDataReceived`** before writing it. An upload
   and `-I` report no data.
2. **Every failure's message is reported as an info line**, before the error line curl prints
   on exit, except exit 55, whose text is only `curl_easy_strerror`'s and has no info line in curl.
   The header-write failure no longer reports its line separately.
3. **A transfer past its open ends with a connection line.** A download that opened its source,
   and every upload (curl opens the destination in the transfer phase), takes a number from the
   handler's own counter. It ends with the failure line (if any), `Progress.ReportTransferDone()`
   (so `-v`'s meter ends its line first), then `closing connection #N` for exit 23 and 26 (libcurl's
   `multi_done` counts those premature) or `shutting down connection #N` otherwise. A missing file
   (exit 37) takes no number and writes no connection line. The texts live in
   `FileTransferMessages`.
4. Numbering shared across schemes in one command line is left to BL-977. Chunk sizes of
   `<= Recv data` for large files are left to BL-976.

## Consequences

- `-v`/`--trace`/`--trace-ascii` for `file://` match curl 8.21.0 byte for byte for the measured
  cases, apart from the `Last-Modified` time in `-I`.
- One Console test (`-v -D -` to a closed standard output) now expects `* closing connection #0`,
  as measured.

## Alternatives considered

- **Report nothing new and pin that.** Lost: the measurement shows curl writes data blocks and a
  connection line, so pinning silence would pin a difference.
- **Share the Console's connection counter.** Lost for now: protocol handlers do not reference
  each other or the Console's pool; the handler's own counter is right for single-scheme command
  lines, and BL-977 tracks mixed ones.
