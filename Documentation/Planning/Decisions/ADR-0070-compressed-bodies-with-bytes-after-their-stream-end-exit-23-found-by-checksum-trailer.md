# ADR-0070 — `--compressed` bodies with bytes after their stream fail with exit 23, the end found from the checksum trailer

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

With `--compressed`, curl decodes the body with zlib or brotli, and treats bytes left over
after the end of the compressed stream as a write failure. Measured against curl 8.21.0
(mingw, Schannel, the Windows reference, ADR-0018) with `Record-CurlExchange.ps1` and
`-s -S --compressed`, `Content-Length` covering the whole body (BL-281):

| Body | curl 8.21.0 |
| --- | --- |
| gzip `hello`, then `41 42` | writes `hello`, `curl: (23) Failed writing received data to disk/application` |
| gzip `hello`, then `1F`, or `1F 8B` | the same |
| gzip `hello`, then a complete gzip member of `world` | the same: the second member is not decoded |
| zlib `hello`, then `41 42` | the same |
| br `hello`, then `41 42` | the same |
| raw deflate `hello`, then `41 42` | writes `hello`, exit 0 |
| raw deflate `hello` alone | writes `hello`, exit 0 |

Curl decodes gzip and deflate with the BCL's `GZipStream`, `ZLibStream` and
`DeflateStream` (ADR-0020). They read their source in blocks and never say how much of the
last block their stream used: bytes after the end are silently dropped, and `GZipStream`
goes on to decode a following gzip member. The base class library has no span-based zlib
decoder; it does have `BrotliDecoder`, which reports the bytes it consumed.

## Decision

- `br` is decoded with `BrotliDecoder` instead of `BrotliStream`. When it reports `Done`
  with bytes left, or bytes arrive after `Done`, the transfer fails with exit 23 after the
  decoded bytes are written.
- The end of a gzip member or a zlib stream is found from its checksum trailer.
  `HttpContentChecksumTrailer` keeps the CRC-32 and size (gzip) or the Adler-32 (zlib) of
  the decoded bytes, hand-rolled because `System.IO.Hashing` is a package, and the first
  place those 8 or 4 bytes appear in the encoded bytes is the end. Bytes after it fail with
  exit 23, whether they arrived with the trailer or later.
  - gzip is searched after every decoded piece, so a second member is stopped before
    anything it decodes is written. An 8-byte match by chance is not a practical risk.
  - zlib is searched only once the stream has finished, told by it no longer reading its
    source, because 4 bytes could match by chance in a long body.
- Bytes after a raw deflate stream are dropped, as measured.

## Consequences

- The measured cases match curl byte for byte, whatever size the body arrives in.
- A gzip or zlib body whose compressed data holds its own trailer's bytes before the real
  trailer, in the last block read, would be cut short and fail with exit 23. It takes a
  crafted body to do that.
- Every decoded gzip byte goes through a table CRC-32, and every zlib byte through
  Adler-32: a small cost next to inflating it.

## Alternatives considered

- **Feed the decompression streams one byte at a time.** Exact, but one native inflate call
  per byte, and decoded output split into tiny writes.
- **Write a managed inflater.** Exact, but a few hundred lines to own, beside a BCL one
  that already works.
- **Leave it.** Exit 0 or 61 where curl gives 23, and the second gzip member written where
  curl writes nothing: not a drop-in replacement.
