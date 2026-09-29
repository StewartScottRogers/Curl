# ADR-0185 — A hand-built Zstandard decoder lives in `Curl.Zstandard.UnitLibrary` and references nothing

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-785.
Amends ADR-0120's table of hand-built libraries with one row.

## Context

Two parts of Curl need to decode Zstandard (RFC 8878) and the base class library has no
decoder for it:

- **HTTP content decoding.** curl 8.21.0 (the reference build, ADR-0018) sends
  `Accept-Encoding: deflate, gzip, br, zstd` for `--compressed` and decodes a
  `Content-Encoding: zstd` body. ADR-0020 drops the `zstd` token "until a zstd decoder
  exists", and FR-073 already states the full header. Decoding lives in
  `Curl.Protocol.Http.UnitLibrary`'s `HttpContentCodingDecoder`, which drives Brotli through
  the BCL's `BrotliDecoder` push API (`OperationStatus Decompress(source, destination, out
  consumed, out written)`) so that it knows where the coded stream ends.
- **TLS certificate decompression (RFC 8879).** The OpenSSL 3.5.5 ClientHello that the
  hand-built TLS client reproduces (ADR-0140) offers `compress_certificate` with zstd
  among its algorithms. BL-786 decodes a `CompressedCertificate` in `Curl.Tls.UnitLibrary`,
  where the whole compressed message is in hand and its `uncompressed_length` (at most
  2^24) is declared up front.

The two consumers sit in different layers. `Curl.Protocol.Http.UnitLibrary` may reference
only Abstractions and ADR-0120's hand-built libraries, and a hand-built library may never
reference a protocol library, so the decoder cannot live in either consumer and be shared.
The standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28) is that a piece the BCL lacks
is hand-built in its own `Curl.<Area>.UnitLibrary` with its own `.UnitTests`, never a
package.

## Decision

### The library

A new hand-built library, **`Curl.Zstandard.UnitLibrary`** (namespace `Curl.Zstandard`),
with **`Curl.Zstandard.UnitTests`** beside it, holds the Zstandard decoder and the XXH64
hash its frames' content checksums use. It **references nothing**: the BCL only, like
`Curl.Cryptography.UnitLibrary`. It takes and returns bytes; it opens no file, socket or
stream of its own, and is AOT-compatible and held to the quality gates like every
production project.

ADR-0120's table gains this row:

| Hand-built library | Holds | May reference |
| --- | --- | --- |
| `Curl.Zstandard.UnitLibrary` | The Zstandard (RFC 8878) decoder and XXH64 | nothing |

It sits at the bottom of the graph beside `Curl.Cryptography`. `Curl.Protocol.Http.UnitLibrary`
and `Curl.Tls.UnitLibrary` may reference it; `Curl.Tls.UnitLibrary`'s row in ADR-0120 gains
`Curl.Zstandard.UnitLibrary` in its "May reference" column, which keeps the graph acyclic
because `Curl.Zstandard` references nothing. `ProtocolIsolationTests` in
`Curl.Protocol.Abstractions.UnitTests` gets the new row and the widened `Curl.Tls` row in
the same change that creates the project, as ADR-0120's "Adding a hand-built library"
requires.

### The API

Shaped after the BCL's `BrotliDecoder`, so `HttpContentCodingDecoder` drives it the way it
already drives Brotli, and TLS gets the one-shot form it needs:

- `public sealed class ZstandardDecoder` with a constructor taking `int maxWindowLog`
  (default `ZstandardDecoder.DefaultMaxWindowLog`, 27) and
  `OperationStatus Decompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesConsumed, out int bytesWritten)`.
  It is a streaming push decoder: input may arrive a byte at a time and output may be
  drained a byte at a time, and it keeps its state between calls.
  - `NeedMoreData` when all of `source` was consumed mid-frame; `DestinationTooSmall` when
    `destination` filled with decoded bytes still to give; `Done` at the end of each frame
    (a Zstandard frame or a skippable frame), after which the next call starts the next
    frame, so a caller decodes concatenated frames (RFC 8878 section 3.1) by calling on
    and learns where the last frame ended, as `HttpContentCodingDecoder` does for Brotli;
    `InvalidData` for anything RFC 8878 forbids, after which the decoder stays failed.
  - `ZstandardDecodeError LastError` names why the last `InvalidData` happened, one enum
    member per libzstd error code curl can surface (for example
    `PrefixUnknown`, `FrameParameterWindowTooLarge`, `ChecksumWrong`, `CorruptionDetected`,
    `DictionaryWrong`), so the HTTP task can map each to the text curl prints after
    measuring it.
- `public static bool TryDecompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten, int maxWindowLog = DefaultMaxWindowLog)`:
  decodes every frame in `source` into `destination`; `false` when the data is invalid,
  when it is truncated, or when the decoded bytes do not fit `destination`. RFC 8879 sizes
  `destination` to the declared `uncompressed_length`, so a lying length or an oversized
  output fails rather than allocating.
- `public static class XxHash64` with `ulong Hash(ReadOnlySpan<byte> data, ulong seed = 0)`
  and an incremental form the decoder feeds as it writes. It is public so its own test
  vectors pin it, not because another library needs it.

### Limits

- **Window.** A frame whose `Window_Size` exceeds `2^maxWindowLog` is `InvalidData`
  (`FrameParameterWindowTooLarge`) before any of its window is allocated. The default,
  2^27 (128 MiB), is libzstd's `ZSTD_WINDOWLOG_LIMIT_DEFAULT`, the limit curl's
  `ZSTD_decompressStream` applies since curl sets none of its own; so a body the reference
  decodes, Curl decodes, and one it refuses, Curl refuses. `maxWindowLog` accepts 10
  (RFC 8878's minimum window) to 31.
- **Memory.** The decoder allocates its window buffer when a frame header names it, at the
  frame's `Window_Size`, or at its `Frame_Content_Size` when `Single_Segment_flag` is set,
  never ahead; a block is at most `min(Window_Size, 128 KiB)` (`Block_Maximum_Size`), and a
  larger block is `InvalidData`.
- **Features.** Every frame RFC 8878 defines: Zstandard frames with or without
  `Frame_Content_Size` and `Content_Checksum` (the checksum is checked, as libzstd does
  by default), skippable frames (magic `0x184D2A5?`, skipped), raw, RLE and compressed
  blocks, raw, RLE, compressed and treeless literals with one or four Huffman streams and
  FSE-compressed Huffman weights, and sequences with predefined, RLE, FSE-compressed and
  repeat tables and the three repeat offsets. A frame naming a `Dictionary_ID` other than 0
  is `InvalidData` (`DictionaryWrong`): neither HTTP content coding nor RFC 8879 has a way
  to agree a dictionary, and libzstd fails the same frame without one. A reserved bit set,
  a declared content size that does not match, or data after a truncated block are
  `InvalidData`.

### Test vectors

- **RFC 8878** itself: the frame and block headers of section 3.1, the predefined FSE
  distributions of section 3.1.1.3.2.2 (Literals_Length, Match_Length and Offset code
  defaults) and the decoding tables of Appendix A, which the predefined tables must equal
  entry for entry, and the Huffman and FSE worked descriptions of section 4.
- **The reference implementation's corpus** (github.com/facebook/zstd, BSD licence):
  `tests/golden-decompression/` (each `.zst` must decode; for example `empty-block.zst`,
  `rle-first-block.zst`, `block-128k.zst`, `zeroSeq_2B.zst`) and
  `tests/golden-decompression-errors/` (each must fail; for example `off0.bin.zst`,
  `zeroSeq_extraneous.zst`, `truncated_huff_state.zst`), copied into
  `Curl.Zstandard.UnitTests` as test data with their source path, the upstream commit and
  the licence notice. `doc/educational_decoder/` is the readable reference the
  implementation tasks check their understanding against; it is read, never copied.
- **XXH64**: the published XXH64 values for the empty input and short sequences with seeds
  0 and a non-zero seed (xxHash's own sanity table), and the content checksum of every
  golden frame that carries one.
- Hand-assembled frames in the tests for each branch the corpus does not reach, so the
  library meets 100% line and branch coverage.

### Who uses it

- `Curl.Protocol.Http.UnitLibrary` references it for `Content-Encoding: zstd`, and
  `--compressed` then advertises `deflate, gzip, br, zstd` as the measured reference does.
  That task supersedes ADR-0020 with a new ADR, measuring first what the Linux and macOS
  OpenSSL builds of curl advertise (a build without libzstd sends no `zstd` token) and what
  curl prints for a corrupt zstd body.
- `Curl.Tls.UnitLibrary` references it for RFC 8879 in BL-786.

## Consequences

- One decoder, correct once, serves both HTTP and TLS, and neither consumer compiles
  against the other.
- The library is pure computation over spans, so its tests need no network, file or clock.
- ADR-0120's graph grows by one leaf that references nothing, so no new path to a socket
  and no cycle is possible.
- A compressed-block decoder is several thousand lines of careful bit-level code (FSE,
  Huffman, sequence execution); the corpus and the coverage gate are what keep it honest.
- Only the decoder is built. Curl never compresses: curl itself has no option that sends a
  zstd-coded body.

## Alternatives considered

- **Put it in `Curl.Cryptography.UnitLibrary`.** Already referenceable from both consumers,
  but compression is not cryptography; the library's name would stop saying what it holds
  ("Say what it does, do what it says"). Rejected.
- **Put it in `Curl.Tls.UnitLibrary`** and let HTTP reference TLS. HTTP would compile
  against a TLS client to decode a body, and HTTP's content decoding would change whenever
  TLS did. Rejected.
- **Put it in `Curl.Protocol.Http.UnitLibrary`.** A hand-built library may never reference
  a protocol library, so TLS could not use it. Rejected.
- **A general `Curl.Compression.UnitLibrary`.** Zlib and Brotli are in the BCL already; the
  only compression Curl hand-builds is Zstandard, so the broader name would promise more
  than it holds. Rejected; if a second hand-built codec is ever needed, it gets its own
  row.
- **A NuGet package (ZstdSharp, ZstdNet).** Forbidden without Stewart's approval, and the
  standing rule says hand-build it. Rejected.
