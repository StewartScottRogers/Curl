# ADR-1365: A deflate zlib stream holds its first decoded bytes until the piece decodes

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation (BL-5889)

## Context

For `Content-Encoding: deflate` with a zlib header, curl 8.22.0 writes nothing a zlib call
decoded when that call fails. When nothing has been written yet, it reads the bytes again
as raw deflate, where a zlib header is a stored block whose lengths disagree. So corrupt
data early in a short body is `invalid stored block lengths`, and `-o` creates no file.
A failed checksum is different: curl has written the decoded bytes by then
(`incorrect data check`). Curl fed the first bytes to `ZLibStream` one at a time and wrote
each decoded piece at once, so it left partial output and gave the generic message.

## Decision

`HttpContentCodingDecoder` holds a `deflate` zlib stream's decoded bytes until the first
piece of the body that decodes to anything is read through, or 16384 bytes are held. Corrupt
data before then discards them and reports the raw deflate text. A failed trailer check
hands them out first. Later pieces, and `gzip` and raw deflate bodies, are handed out as
before.

## Consequences

- Both measured cases (corrupt deflate data, and a checksum reached early) match real curl.
- The BCL streams do not report block boundaries, which curl's zlib stops at. A first
  piece whose first block is whole and whose second is corrupt writes nothing here and says
  `invalid stored block lengths`, where curl writes the first block. Matching that needs a
  hand-built inflate, which this case does not justify on its own.
