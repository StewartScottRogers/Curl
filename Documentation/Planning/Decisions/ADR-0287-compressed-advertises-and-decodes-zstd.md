# ADR-0287 — `--compressed` advertises `deflate, gzip, br, zstd` and decodes `zstd`

- **Status:** Accepted
- **Date:** 2026-09-30

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-861.
Supersedes ADR-0020.

## Context

ADR-0020 left `zstd` out of `--compressed`'s `Accept-Encoding` until Curl could decode it.
The hand-built Zstandard decoder now exists: `ZstandardDecoder` in
`Curl.Zstandard.UnitLibrary` (BL-857 to BL-860, ADR-0185), a push decoder shaped after
the BCL's `BrotliDecoder`, which ADR-0120 (as amended) lets `Curl.Protocol.Http.UnitLibrary`
reference.

Measured with `Record-CurlExchange.ps1` (BL-861 Notes), curl 8.21.0 Schannel (mingw64),
`-sS --compressed`, and Linux curl 8.18.0 OpenSSL (WSL Ubuntu) for the header and the
trailing-bytes case:

- Both builds send `Accept-Encoding: deflate, gzip, br, zstd` after `Accept: */*`.
  macOS's OpenSSL build (Homebrew curl links brotli and zstd) is cited, not measured.
- A valid frame writes its content, exit 0; two concatenated frames write both.
- A body that ends mid-frame writes what its blocks gave, exit 0.
- A bad magic number or a reserved block type writes nothing and is exit 61
  `Unrecognized or bad HTTP Content or Transfer-Encoding`.
- Bytes after the last frame (`junk`, or four zero bytes) write the frame's content and
  then fail with exit 61, not the exit 23 gzip and Brotli give: libzstd reads them as the
  next frame's magic number and rejects it.

## Decision

1. `HttpRequestHeadFormatter.AcceptEncoding` is `deflate, gzip, br, zstd` on every
   platform, the value both measured builds send.
2. `Content-Encoding: zstd` (any letter case) maps to `HttpContentCoding.Zstandard`, and
   `HttpContentCodingDecoder` decodes it with `ZstandardDecoder` frame after frame: on
   `Done` with bytes left, it carries on with the next frame; `InvalidData` is exit 61
   `BadContentEncoding`; a body that ends mid-frame is not an error.
3. Bytes after the last frame are therefore never "bytes after the end" (exit 23) for
   zstd; they are the start of a frame that is either valid or corrupt, as in libzstd.

## Consequences

- `Curl.Protocol.Http.UnitLibrary` references `Curl.Zstandard.UnitLibrary`.
- Every test that pins the `--compressed` request header pins the four-token value.
- Fewer than four bytes after the last frame are held as the start of a magic number and
  dropped when the body ends, as libzstd waits for more input.
