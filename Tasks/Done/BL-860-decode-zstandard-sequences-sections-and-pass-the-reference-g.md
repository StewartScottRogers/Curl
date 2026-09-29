---
id: BL-860
title: Decode Zstandard sequences sections and pass the reference golden corpus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-859]
touches: [Curl.Zstandard.UnitLibrary, Curl.Zstandard.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-860 — Decode Zstandard sequences sections and pass the reference golden corpus

## Goal

`ZstandardDecoder` decodes the sequences section of a compressed block (predefined, RLE, FSE-compressed and repeat tables for literal lengths, match lengths and offsets, the three repeat offsets, sequence execution across the window) and decodes every file of the reference implementation's golden corpus, so it is a complete RFC 8878 decoder.

## Context

- ADR-0185 (BL-785); builds on BL-858 (frames) and BL-859 (literals, FSE tables).
- RFC 8878 section 3.1.1.3.2 (sequences section, `Symbol_Compression_Modes`, predefined distributions 3.1.1.3.2.2), 3.1.1.4 (sequence execution), 3.1.1.5 (repeat offsets), Appendix A (predefined decoding tables, which the tables built from the default distributions must equal entry for entry).
- Corpus (github.com/facebook/zstd, BSD licence): every file under `tests/golden-decompression/` must decode and every file under `tests/golden-decompression-errors/` must be `InvalidData`; copy them into `Curl.Zstandard.UnitTests` as test data with their source path, upstream commit and licence notice.
- Match copies may overlap their own output (offset smaller than length) and may reach back into previous blocks of the frame up to `Window_Size`.
- BL-786 (RFC 8879 in `Curl.Tls`) and BL-861 (HTTP `Content-Encoding: zstd`) wait on this task.

## Acceptance criteria

- [x] The tables built from RFC 8878's default distributions equal Appendix A's decoding tables for literal lengths, match lengths and offsets.
- [x] Hand-assembled blocks exercising each `Symbol_Compression_Modes` value, each repeat-offset rule (including the literal-length-zero shift), and an overlapping match decode to the expected bytes.
- [x] Every copied file from `tests/golden-decompression/` decodes (its content checksum checked where present), and every copied file from `tests/golden-decompression-errors/` is `InvalidData`, each through `TryDecompress` and through `Decompress` fed one byte at a time.
- [x] An offset reaching before the start of the window, a match past `Frame_Content_Size`, and leftover bits in the sequence bitstream are each `InvalidData` with `CorruptionDetected`.
- [x] The library meets the quality gates.

## Notes

- Plan, as built: `ZstandardSequencesDecoder` reads `Number_of_Sequences`, the modes and
  the three tables (`ZstandardSequenceField` holds each code's limits and predefined
  table; `ZstandardFseTable.Rle` builds `RLE_Mode` tables), then runs the FSE bitstream;
  `ZstandardSequenceValues` turns codes into lengths and offsets, `ZstandardSequenceWriter`
  executes each sequence (overlapping matches copy in doubling runs), and
  `ZstandardHistory` is the frame's ring buffer of past content. `ZstandardDecoder` now
  executes every compressed block into a block buffer and appends all content it gives to
  the history. `SequencesNotYetSupported` is gone.
- Decision (ADR-0193): real curl decodes matches that reach past `Window_Size` as long as
  libzstd still buffers the bytes (measured: 1025 back in a 1 KiB window after 2000 bytes,
  and 3040 back after 3072, both right and exit 0), so the history holds libzstd's buffer
  size, `Window_Size + 2 * Block_Maximum_Size + 64`, rather than refusing past the window.
- Measured: all 34 hand-assembled frames of `ZstandardSequencesDecoderTests` went through
  real curl 8.21.0 (libzstd 1.5.7) with `Record-CurlExchange.ps1`: the 16 valid ones print
  the same bytes, the 18 malformed ones exit 61.
- The golden corpus has no decodable frame with sequences, so `LibzstdFrames/` adds five
  frames libzstd 1.5.7 compressed (via Windows' bsdtar) at levels -5 to 19, up to 400000
  bytes over four blocks; each decodes to the SHA-256 of its source, whole and a byte at a
  time.
- `off0.bin.zst` needed `WithCulture="false"` on the `EmbeddedResource`: MSBuild read
  `.bin` as the Bini culture and silently moved the file into a satellite assembly.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0193 and its index row; no
  task in Doing names it.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ZstandardDecoder decodes sequences sections and every file of the zstd golden corpus: a complete RFC 8878 decoder
