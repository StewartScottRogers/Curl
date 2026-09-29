---
id: BL-859
title: Decode Zstandard literals sections with Huffman and FSE-compressed weights
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-858]
touches: [Curl.Zstandard.UnitLibrary, Curl.Zstandard.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
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

- [x] Hand-assembled compressed blocks with zero sequences decode to the expected bytes for raw literals, RLE literals, Huffman literals with one stream and with four streams, direct-weight and FSE-compressed-weight tree descriptions, and treeless literals reusing the previous block's tree.
- [x] Treeless literals with no previous tree, a Huffman stream whose padding or final state is wrong, weights that do not form a complete tree, and a literals size beyond the block are each `InvalidData` with `CorruptionDetected`.
- [x] Decoding through `Decompress` one byte at a time gives the same output as `TryDecompress`.
- [x] The library meets the quality gates.

## Notes

- **Shape.** New internal types in `Curl.Zstandard.UnitLibrary`: `ZstandardBackwardBitReader`
  and `ZstandardForwardBitReader` (with `ZstandardBitLoad`), `ZstandardFseTable` (general:
  `Read` a table description, `Build` from a distribution; BL-860 reuses it),
  `ZstandardHuffmanTable` (tree description with direct or FSE weights, stream decoding),
  `ZstandardLiteralsHeader`/`ZstandardLiteralsType`, and `ZstandardLiteralsDecoder`, which
  keeps the frame's last Huffman table for treeless literals and forgets it at each new
  frame. `ZstandardDecoder` gathers a whole compressed block (a 128 KiB buffer, allocated at
  the first compressed block), decodes its literals, checks that `Number_of_Sequences` is 0
  (1-byte or 2-byte form, nothing after it), and gives the literals out as the destination
  allows. No window buffer yet: nothing refers back until sequences do (BL-860).
- **Decisions (ADR-0192).** A compressed block of `Block_Size` 0 is an empty block: measured,
  curl 8.21.0/libzstd 1.5.7 decodes it (the streaming libzstd path), though the one-shot API
  would not. Four streams of fewer than 6 literals are the new `LiteralsHeaderWrong`
  (libzstd `literals_headerWrong`). `CompressedBlockNotYetSupported` is renamed
  `SequencesNotYetSupported` for blocks with sequences, until BL-860. Following libzstd's
  `HUF_readStats`, a tree needs at least two weight-1 symbols.
- **Test vectors.** `ZstandardTestLiterals` (test project) is a test-only encoder for
  literals sections. Every valid frame the tests build (21) was served to real curl 8.21.0 as
  a `Content-Encoding: zstd` body with `Record-CurlExchange.ps1` and decoded to the expected
  bytes; every invalid one (34) failed with exit 61. Two are pinned as measured hex. The
  predefined Literals_Length table built by `ZstandardFseTable.Build` equals RFC 8878
  Appendix A.1 state for state, and the Huffman table reproduces RFC 8878 section 4.2.1's
  worked prefix codes.
- **Corpus.** From github.com/facebook/zstd at `01b7154f1172432f8abe9b3bb9909e14a1176b7d`:
  `zeroSeq_2B.zst`, `empty-block.zst`, `block-128k.zst` (a compressed block of raw literals),
  `rle-first-block.zst` decode; `truncated_huff_state.zst` and `zeroSeq_extraneous.zst` fail
  with `CorruptionDetected`. They are embedded resources (no file access in the fast tests),
  with provenance and the BSD licence in `Curl.Zstandard.UnitTests/GoldenDecompression/README.md`,
  and `*.zst binary` in the project's `.gitattributes`. `off0.bin.zst` holds sequences and is
  left for BL-860.
- **Review.** The code-reviewer found that FSE count reading added 1 to the remaining
  probability twice (libzstd's `+1` start with the educational decoder's formula); the
  test encoder shared the mistake, so the tests agreed with each other. Fixed, pinned with
  libzstd's own description `D0 0F` of counts {28, 4}, and a count-of-28 vector added and
  measured in curl.
- **Touches.** Added `Documentation/Planning/Decisions` for ADR-0192 and its index row;
  no task in `Doing` names it.

- **Integration (second run, lane 2).** Lane 1's work was cherry-picked from
  `factory/BL-859-lane-1-20260929-023709`. Its ADR had been numbered 0191, which BL-610 took
  for `--cert-status` in the meantime; renumbered to ADR-0192 here and in its three
  references. After the rebase the build is clean and every fast test assembly passes
  (Zstandard 185/185), so the earlier integration failure did not reproduce.


## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 1 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-859-lane-1-20260929-023709; start with git cherry-pick --no-commit factory/BL-859-lane-1-20260929-023709 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Literals sections decode (raw, RLE, one/four Huffman streams, direct and FSE weights, treeless); ADR renumbered to 0192; build clean, fast tests green
