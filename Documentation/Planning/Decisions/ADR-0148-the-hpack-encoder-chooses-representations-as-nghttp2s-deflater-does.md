# ADR-0148 — The HPACK encoder chooses representations as nghttp2's deflater does

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-656.

## Context

RFC 7541 fixes how an HPACK header block is decoded but leaves the encoder free: any field
may be an index, a literal with or without indexing, or a literal never indexed, and any
string may or may not be Huffman-coded. Every choice changes the bytes on the wire.
ADR-0141 makes HTTP/2 hand-built in `Curl.Http2.UnitLibrary` and has every platform's
curl speak it through nghttp2 (1.68.0 in the measured builds), but it does not say how the
encoder chooses. A drop-in replacement should send what curl sends.

Measured 2026-09-28 with curl.se's Windows build (curl 8.18.0, nghttp2 1.68.0), through
`Record-CurlExchange.ps1` and `--http2-prior-knowledge`: the HEADERS block for a GET with
`Authorization`, a short `Cookie` and a repeated custom header is 90 bytes, pinned in
`HpackEncoderTests.CurlHeaderBlock`. It shows `:path` as a literal without indexing,
`authorization` and the 3-byte `cookie` as literals never indexed, `user-agent`,
`accept`, `:authority` and the custom header as literals with incremental indexing, the
repeated custom header as a dynamic index, `*/*` sent raw and everything else Huffman-coded.
curl sends no SETTINGS_HEADER_TABLE_SIZE, so both tables start at 4096.

## Decision

`HpackEncoder` follows nghttp2's deflater (`nghttp2_hd.c`, `deflate_nv`):

1. **Never indexed:** `authorization`, `proxy-authorization`, a `cookie` shorter than 20
   bytes, and any field marked `HeaderField.IsNeverIndexed`. Such a field is never sent as
   a full index either, even when a table holds it; only its name is looked up.
2. **Without indexing:** `:path`, `age`, `content-length`, `etag`, `if-modified-since`,
   `if-none-match`, `location`, `set-cookie`, and any field whose size exceeds three
   quarters of the table's maximum size.
3. **Incremental indexing:** every other field.
4. **Lookup order:** an exact name-and-value match in the dynamic table, then the static
   table (exact match, else the first entry with the name), then the newest dynamic entry
   with the name, else a new name.
5. **Huffman** when the coded string is strictly shorter than the raw one.
6. **Table size:** the encoder never uses more than 4096 bytes whatever the peer allows.
   A new SETTINGS_HEADER_TABLE_SIZE takes effect at once, and the next block opens with
   the smallest size passed through (when below the final one) and then the final one.

`HpackDecoder` accepts every representation RFC 7541 allows and reports each broken rule
as an `HpackDecodingException` with its own `HpackDecodingError`. It also enforces what
nghttp2's inflater enforces: a size update only at the start of a block, and one required
there after the local limit was lowered.

## Consequences

- The request header block is byte-for-byte what curl sends for the measured case, and the
  RFC 7541 appendix C.4 blocks come out exactly as published.
- Header names arrive lowercased from the HTTP/2 layer (BL-658); the encoder does not
  lowercase them.
- `HpackHuffman` is public so QPACK (BL-729) reuses it.
