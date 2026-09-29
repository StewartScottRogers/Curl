---
id: BL-859
title: Decode Zstandard literals sections with Huffman and FSE-compressed weights
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-858]
touches: [Curl.Zstandard.UnitLibrary, Curl.Zstandard.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-859 — Decode Zstandard literals sections with Huffman and FSE-compressed weights

## Goal

`ZstandardDecoder` decodes the literals section of a compressed block (raw, RLE, compressed and treeless literals, one or four Huffman streams, Huffman tree descriptions with direct or FSE-compressed weights), so a compressed block with zero sequences decodes.

## Context

- ADR-0185 (BL-785); builds on BL-858's frame and block layer.
- RFC 8878 section 3.1.1.3.1 (literals section header and sizes), 4.2 (Huffman coding, tree description, weights), 4.1 (FSE, needed here for weight decoding and reused by BL-860).
- The FSE table decoder written here is the one BL-860 reuses for sequences; keep it general.
- `doc/educational_decoder/` in github.com/facebook/zstd is the readable reference to check understanding against; read it, do not copy it.
- Test vectors: hand-assembled blocks, plus from the reference corpus `tests/golden-decompression/` any file that exercises literals only (for example `zeroSeq_2B.zst`), copied as ADR-0185 says (source path, upstream commit, licence notice).

## Acceptance criteria

- [ ] Hand-assembled compressed blocks with zero sequences decode to the expected bytes for raw literals, RLE literals, Huffman literals with one stream and with four streams, direct-weight and FSE-compressed-weight tree descriptions, and treeless literals reusing the previous block's tree.
- [ ] Treeless literals with no previous tree, a Huffman stream whose padding or final state is wrong, weights that do not form a complete tree, and a literals size beyond the block are each `InvalidData` with `CorruptionDetected`.
- [ ] Decoding through `Decompress` one byte at a time gives the same output as `TryDecompress`.
- [ ] The library meets the quality gates.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-859-lane-1-20260929-023709; start with git cherry-pick --no-commit factory/BL-859-lane-1-20260929-023709 and fix it.
