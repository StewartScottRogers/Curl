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
completed:
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

- [ ] `XxHash64.Hash` matches published xxHash values for the empty input and at least three other inputs, with seed 0 and a non-zero seed, and its incremental form gives the same value when fed a byte at a time.
- [ ] A hand-assembled frame of raw blocks, one of RLE blocks, one with `Single_Segment_flag`, one for each `Frame_Content_Size` field size (0, 1, 2, 4, 8 bytes), and two concatenated frames with a skippable frame between them each decode to the expected bytes through `TryDecompress` and through `Decompress` fed one byte at a time; `Decompress` returns `Done` at each frame's end.
- [ ] Each of these is `InvalidData` with the named `LastError`: an unknown magic (`PrefixUnknown`), a window above `2^maxWindowLog` (`FrameParameterWindowTooLarge`), a non-zero `Dictionary_ID` (`DictionaryWrong`), a wrong checksum (`ChecksumWrong`), a reserved bit set, a block larger than `Block_Maximum_Size`, a content size that does not match, and a compressed block (until BL-859 and BL-860).
- [ ] `TryDecompress` returns false for truncated input and for output that does not fit `destination`.
- [ ] The library meets the quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30).

## Notes

## Log

- 2026-09-29: Created.
