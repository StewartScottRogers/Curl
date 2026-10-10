# ADR-0471: a decoded piece that would pass --max-filesize is not written

- Status: Accepted
- Date: 2026-10-10
- Decided by Claude under Stewart's delegation.
- Task: BL-2007 (gap finding GF-0014, re-closed)
- Amends: ADR-0442 (its decoded-bytes bullet; the discarded-body bullet stands)

## Context

ADR-0442 wrote as many decoded bytes as `--max-filesize` allowed before failing with exit
63, and left the exact bytes to the gap run's re-measure. That re-measure still failed
upstream test1618: curl wrote none of the bomb's decoded bytes. Measured against the
platform's curl 8.21.0 (Schannel, `C:\Windows\System32\curl.exe`) with a 100,000-byte gzip
bomb, `--compressed -o file`:

- `--max-filesize 1000`: exit 63, `curl: (63) Would have exceeded max file size`, no
  output file created.
- `--max-filesize 20000`: the same exit and message, and the file holds 16384 bytes - the
  first whole decoded piece (curl decodes into 16 KiB buffers), none of the second.

## Decision

`HttpContentDecoder` checks each decoded piece (at most `HttpContentCodingDecoder.OutputSize`,
16384 bytes, as curl's buffer) before writing it: a piece that would take the delivered
bytes past the limit is not written, and the transfer ends with exit 63 and
`Would have exceeded max file size` (`HttpTransferMessages.DecodedFileSizeLimitExceeded`).
Pieces wholly under the limit are written as before. The undecoded path keeps its
`Exceeded the maximum allowed file size (<limit>) with <n> bytes` message.

## Consequences

- Curl matches real curl byte for byte at both measured limits, including no output file
  when nothing was written.
- test477 (the discarded redirect body) was re-measured with Content-Length, chunked,
  close-delimited and kept-alive redirect bodies; every shape already matched real curl, so
  ADR-0442's first bullet is unchanged.
