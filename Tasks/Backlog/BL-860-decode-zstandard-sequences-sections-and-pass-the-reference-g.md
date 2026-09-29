---
id: BL-860
title: Decode Zstandard sequences sections and pass the reference golden corpus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-859]
touches: [Curl.Zstandard.UnitLibrary, Curl.Zstandard.UnitTests]
requirement: none
created: 2026-09-29
completed:
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

- [ ] The tables built from RFC 8878's default distributions equal Appendix A's decoding tables for literal lengths, match lengths and offsets.
- [ ] Hand-assembled blocks exercising each `Symbol_Compression_Modes` value, each repeat-offset rule (including the literal-length-zero shift), and an overlapping match decode to the expected bytes.
- [ ] Every copied file from `tests/golden-decompression/` decodes (its content checksum checked where present), and every copied file from `tests/golden-decompression-errors/` is `InvalidData`, each through `TryDecompress` and through `Decompress` fed one byte at a time.
- [ ] An offset reaching before the start of the window, a match past `Frame_Content_Size`, and leftover bits in the sequence bitstream are each `InvalidData` with `CorruptionDetected`.
- [ ] The library meets the quality gates.

## Notes

## Log

- 2026-09-29: Created.
