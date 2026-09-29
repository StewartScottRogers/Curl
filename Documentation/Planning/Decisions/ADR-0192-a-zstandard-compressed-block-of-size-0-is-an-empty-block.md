# ADR-0192 — A Zstandard compressed block of size 0 is an empty block, and literals errors are named after libzstd's

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-859.
Refines ADR-0185's "Features" for the literals section.

## Context

BL-859 taught `ZstandardDecoder` (`Curl.Zstandard.UnitLibrary`) to decode a compressed
block's literals section. Three behaviours had to be settled where RFC 8878 is silent or
libzstd's two decoding paths differ:

- **A compressed block whose `Block_Size` is 0.** RFC 8878 gives a compressed block at
  least a literals section header and a sequences section header, so 0 bytes cannot hold
  one. libzstd's one-shot `ZSTD_decompress` fails such a block (`corruption_detected`), but
  its streaming decoder (`ZSTD_decompressStream`, through `ZSTD_decompressContinue`)
  treats a 0-byte block of any type as an empty block and moves on. curl drives the
  streaming decoder. Measured with `Record-CurlExchange.ps1`: curl 8.21.0 with libzstd
  1.5.7 given a `Content-Encoding: zstd` body of a 0-byte compressed block, a raw block of
  "ok" and a last 0-byte compressed block prints "ok" and exits 0.
- **Four Huffman streams regenerating fewer than 6 literals.** libzstd refuses it with
  `literals_headerWrong` (`MIN_LITERALS_FOR_4_STREAMS`), not `corruption_detected`.
- **A compressed block that holds sequences**, which BL-860 decodes. Until then it needs
  its own error, and the BL-858 member `CompressedBlockNotYetSupported` no longer says
  what fails.

## Decision

- A compressed block with `Block_Size` 0 decodes to nothing, as curl's streaming libzstd
  does; the next block (or the checksum, after a last block) follows. HTTP content
  decoding reproduces curl, and one decoder serves both consumers (ADR-0185), so both
  follow curl.
- `ZstandardDecodeError` gains `LiteralsHeaderWrong` for four streams of fewer than 6
  literals. Every other malformed literals section - treeless literals with no earlier
  tree in the frame, a Huffman stream without its padding bit or not ending where its last
  literal does, weights that do not complete a tree of codes at most 11 bits, a tree
  description, jump table or literals running past the block, literals larger than
  `Block_Maximum_Size` or the declared content - is `CorruptionDetected`, as in libzstd.
  curl reports each the same way: exit 61, "Unrecognized or bad HTTP Content or
  Transfer-Encoding" (measured).
- `CompressedBlockNotYetSupported` is renamed `SequencesNotYetSupported`: a compressed
  block whose `Number_of_Sequences` is not 0. BL-860 removes it. A `Number_of_Sequences` of
  0, in its 1-byte or 2-byte form, must end the block; a byte after it is
  `CorruptionDetected`, as libzstd's `zeroSeq_extraneous.zst` requires.
- A Huffman tree needs at least two symbols of weight 1 (its longest codes), as libzstd's
  `HUF_readStats` requires; a tree without them is `CorruptionDetected`, though RFC 8878
  does not say so.
- The Huffman table of a frame's last compressed literals is kept for treeless literals
  across raw and RLE literals and blocks, and forgotten when a new frame starts.

## Consequences

- Every hand-built literals frame in `Curl.Zstandard.UnitTests` decodes in real curl to
  the same bytes, and every malformed one fails there too (measured for BL-859), so the
  decoder and the tests' encoder are checked against the reference, not against each other.
- The decoder accepts one frame the one-shot libzstd API refuses. Only a caller comparing
  against `ZSTD_decompress` could notice, and Curl has none.

## Alternatives considered

- **Fail a 0-byte compressed block, as RFC 8878's layout and one-shot libzstd imply.**
  curl would print the body and Curl would exit 61 on it: not a drop-in replacement.
  Rejected.
- **Report four short streams as `CorruptionDetected`.** Simpler, but the enum's rule is
  one member per libzstd error code, so a caller mapping errors to text would be told the
  wrong code. Rejected.
