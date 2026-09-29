# Curl.Zstandard.UnitLibrary

Hand-built Zstandard (RFC 8878) for every platform, per ADR-0185: the frame decoder and
the XXH64 hash its content checksums use. The base class library has no Zstandard
decoder, and two layers need one - HTTP content decoding (`Content-Encoding: zstd` in
`Curl.Protocol.Http.UnitLibrary`) and TLS certificate decompression (RFC 8879 in
`Curl.Tls.UnitLibrary`) - so it lives in neither and both may reference it (ADR-0120's
table, as amended by ADR-0185).

Namespace `Curl.Zstandard`. What it holds (BL-858, BL-859, BL-860), a complete RFC 8878
decoder that decodes every frame of the reference implementation's golden corpus:

- `XxHash64` (one-shot) and `XxHash64Accumulator` (incremental), pinned to xxhsum's
  sanity table and python-xxhash's published values.
- `ZstandardDecoder`: the streaming push decoder of ADR-0185 (`Decompress` returning
  `OperationStatus`, `LastError`, static `TryDecompress`). It decodes frames of raw, RLE
  and compressed blocks, skips skippable frames and checks `Content_Checksum`. A
  compressed block is gathered whole, its literals and sequences sections decoded and its
  sequences executed into a block buffer before any of it is given. A 0-byte compressed
  block is an empty block, as curl's streaming libzstd treats it (ADR-0192).
- `ZstandardSequencesDecoder` decodes a sequences section (`Number_of_Sequences`,
  `Symbol_Compression_Modes`, predefined, RLE, FSE-compressed and repeated tables, the FSE
  bitstream) and keeps the frame's last tables and three repeat offsets;
  `ZstandardSequenceField` holds each code's limits and predefined table (pinned to RFC
  8878 Appendix A), `ZstandardSequenceValues` turns codes into lengths and offsets, and
  `ZstandardSequenceWriter` executes the sequences.
- `ZstandardHistory` keeps the frame's latest content for matches to copy from: as much as
  libzstd's streaming decoder buffers, `Window_Size` plus two `Block_Maximum_Size` plus 64
  bytes, so a match may reach back that far, as in curl (ADR-0196).
- `ZstandardLiteralsDecoder` decodes a literals section (raw, RLE, compressed, treeless;
  one or four Huffman streams) and keeps the frame's last Huffman table for treeless
  literals; `ZstandardLiteralsHeader` and `ZstandardLiteralsType` read its header.
- `ZstandardHuffmanTable` reads a Huffman tree description (direct or FSE-compressed
  weights) and decodes a Huffman stream.
- `ZstandardFseTable` reads an FSE table description and builds its decoding table, for
  Huffman weights and for sequences alike; `Rle` builds a sequences section's `RLE_Mode`
  table.
- `ZstandardBackwardBitReader` and `ZstandardForwardBitReader` read the bitstreams;
  `ZstandardBitLoad` loads their bytes.
- `ZstandardFrameHeader` reads the frame header; `ZstandardDecodeError` names each failure
  after the libzstd error code.

Tests: `ZstandardTestLiterals` and `ZstandardTestSequences` in `Curl.Zstandard.UnitTests`
are test-only encoders for literals and sequences sections. Every frame they build for the
tests was measured in real curl 8.21.0 (libzstd 1.5.7) with `Record-CurlExchange.ps1`: the
valid ones decode to the same bytes, the malformed ones fail with exit 61. The reference
corpus frames and frames libzstd compressed from known content are embedded resources,
with provenance in `GoldenDecompression/README.md` and `LibzstdFrames/README.md`.

## Rules

- **Base class library only; references nothing.** No package and no project reference.
  `ProtocolIsolationTests` in `Curl.Protocol.Abstractions.UnitTests` fails the build's
  tests if this project references any other.
- **Bytes in, bytes out.** The library opens no file, socket or stream of its own; its
  callers hand it the coded bytes.
- Tests in `Curl.Zstandard.UnitTests` are platform-neutral.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
