# ADR-0824: gzip and zlib trailer failures write the decoded bytes and name the check

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3600)
- Relates to: [ADR-0031](ADR-0031-corrupt-compressed-bodies-report-curls-generic-exit-61-text-where-zlib-text-is-unavailable.md)

## Context

CGR-071: for a `gzip` body whose CRC-32 or size does not match, curl 8.22.0 writes every decoded
byte and then fails with exit 61 and zlib's text, `Error while processing content unencoding:
incorrect data check` (CRC-32) or `... incorrect length check` (size); a zlib stream with a bad
Adler-32 is `incorrect data check` too. Curl wrote nothing and gave the generic text: the BCL's
`GZipStream` and `ZLibStream` throw `InvalidDataException` on a failed check and drop what that
read decoded, and the exception says nothing of which check failed (measured: "The archive entry
was compressed using an unsupported compression method." for a bad CRC, a bad size and corrupt
deflate data alike).

## Decision

- `HttpContentCodingDecoder` feeds the first and last 8 encoded bytes of each piece of the body to
  the decompression stream one byte at a time and the bytes between at once. A trailer ends the
  body, or spans the piece before, so it is always fed alone and its failing read decodes nothing:
  every decoded byte is written before the failure.
- When the stream fails on a byte fed alone after it decoded something,
  `HttpContentChecksumTrailer.CheckFailed` names the check from where it failed, counted from the
  last read that decoded something (the end-of-block code takes at most 3 more bytes): 4 to 7
  bytes on, with those 4 bytes not the checksum, is `incorrect data check`; for gzip, 8 to 11
  bytes on, with the 4 bytes before them the matching CRC-32, is `incorrect length check`.
  Anything else keeps ADR-0031's generic text.
- Since a trailer fed alone decodes nothing, the end of a gzip member is also looked for after
  each feed, once something was decoded, so bytes after it still fail with exit 23 before
  `GZipStream` reads them as a second member.

## Consequences

- The repros match curl 8.22.0 (Schannel) byte for byte; the OpenSSL build uses the same zlib
  text, so Linux and macOS match too.
- Corrupt deflate data that fails within a trailer's reach of its last decoded byte can be named
  `incorrect data check` where zlib would name the deflate error; before, it was the generic text.
- Up to 16 extra stream reads per piece of the body, and decoded output can arrive in one more,
  smaller piece.
