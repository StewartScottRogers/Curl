---
id: BL-858
title: Decode Zstandard frames with raw and RLE blocks, skippable frames and the XXH64 checksum
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-857]
touches: [Curl.Zstandard.UnitLibrary, Curl.Zstandard.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-858 — Decode Zstandard frames with raw and RLE blocks, skippable frames and the XXH64 checksum

## Goal

`ZstandardDecoder` (ADR-0185's API: push `Decompress` returning `OperationStatus`, `LastError`, static `TryDecompress`) decodes every RFC 8878 frame whose blocks are raw or RLE, skips skippable frames, and checks `Content_Checksum` with a hand-built `XxHash64`; a compressed block is `InvalidData` until BL-859 and BL-860.

## Context

- ADR-0185 fixes the API, the window limit (`DefaultMaxWindowLog` 27, `maxWindowLog` 10 to 31), allocation at frame-header time, `Block_Maximum_Size`, and `DictionaryWrong` for a non-zero `Dictionary_ID`.
- RFC 8878 section 3.1.1 (frame header: magic `0xFD2FB528`, `Frame_Header_Descriptor`, `Window_Descriptor`, `Dictionary_ID`, `Frame_Content_Size`, `Content_Checksum` as the low 32 bits of XXH64 seed 0), 3.1.1.2 (blocks), 3.1.2 (skippable frames `0x184D2A50` to `0x184D2A5F`).
- XXH64: the xxHash specification (github.com/Cyan4973/xxHash, `doc/xxhash_spec.md`); pin published values for the empty input and short inputs with seed 0 and a non-zero seed.
- Streaming must work with input and output delivered one byte per call.

## Acceptance criteria

- [x] `XxHash64.Hash` matches published xxHash values for the empty input and at least three other inputs, with seed 0 and a non-zero seed, and its incremental form gives the same value when fed a byte at a time.
- [x] A hand-assembled frame of raw blocks, one of RLE blocks, one with `Single_Segment_flag`, one for each `Frame_Content_Size` field size (0, 1, 2, 4, 8 bytes), and two concatenated frames with a skippable frame between them each decode to the expected bytes through `TryDecompress` and through `Decompress` fed one byte at a time; `Decompress` returns `Done` at each frame's end.
- [x] Each of these is `InvalidData` with the named `LastError`: an unknown magic (`PrefixUnknown`), a window above `2^maxWindowLog` (`FrameParameterWindowTooLarge`), a non-zero `Dictionary_ID` (`DictionaryWrong`), a wrong checksum (`ChecksumWrong`), a reserved bit set, a block larger than `Block_Maximum_Size`, a content size that does not match, and a compressed block (until BL-859 and BL-860).
- [x] `TryDecompress` returns false for truncated input and for output that does not fit `destination`.
- [x] The library meets the quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30).

## Notes

- Plan: `XxHash64` (one-shot) and `XxHash64Accumulator` (incremental) share `XxHash64Lanes`
  (the four striped accumulators) and `XxHash64.Finish`; `ZstandardFrameHeader` reads the
  header fields; `ZstandardDecoder` is a byte-granular state machine (magic, skippable size
  and content, descriptor, header fields, block header, raw block, RLE value, RLE block,
  checksum) gathering each fixed-size field into a 13-byte buffer, so any split of input or
  output works.
- Measured: curl 8.21.0 (Schannel, libzstd 1.5.7) decodes the hand-built frame
  `28B52FFD240C61000048656C6C6F2C20776F726C64D7B42074` (single segment, raw block,
  checksum) to `Hello, world` as a `Content-Encoding: zstd` body with `--compressed`, and
  with the checksum's last byte 0x75 prints `curl: (61) Unrecognized or bad HTTP Content or
  Transfer-Encoding` and exits 61 (Record-CurlExchange.ps1). So the checksum is libzstd's.
  The frame is pinned in `ZstandardDecoderTests`.
- XXH64 vectors: xxhsum's sanity table uses a buffer whose generator is multiplied by
  11400714785074694797 (xxhsum's own `PRIME64`, not `XXH_PRIME64_1`); python-xxhash's
  README gives `xxhash` (seeds 0 and 20141025) and "Nobody inspects the spammish repetition".
- Choices (sensible defaults, within ADR-0185):
  - A compressed block fails with a new `ZstandardDecodeError.CompressedBlockNotYetSupported`
    rather than borrowing `CorruptionDetected`, because the block is not corrupt; BL-859 and
    BL-860 remove the member when they decode compressed blocks.
  - No window buffer is allocated yet: raw and RLE blocks go straight to the destination.
    The window check (`FrameParameterWindowTooLarge`) is made at frame-header time as
    ADR-0185 says; allocation arrives with the compressed-block tasks, which need it.
  - libzstd's check order: reserved bit (`FrameParameterUnsupported`) at the descriptor,
    then `DictionaryWrong`, then `FrameParameterWindowTooLarge`; a block's reserved type and
    oversize are `CorruptionDetected` before a compressed type is reported; content beyond
    `Frame_Content_Size` fails at the block header that would exceed it, short content at
    the last block.
  - When a raw block's source runs out, `Decompress` returns `NeedMoreData` even if the
    destination is also full; `DestinationTooSmall` only when decoded bytes are ready and
    have no room.
  - `TryDecompress` of an empty source returns true with 0 bytes, as libzstd's
    `ZSTD_decompress` does.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Zstandard.UnitLibrary`: 100% line, 100%
  branch, 47 members, worst CRAP 9. 88 tests in `Curl.Zstandard.UnitTests`; code-reviewer found no defects, and its three suggested tests (a window mantissa, a magic above the skippable range, source and destination running out together) were added.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ZstandardDecoder decodes raw and RLE block frames, skips skippable frames and checks Content_Checksum with a hand-built XXH64, a byte at a time or in one call
