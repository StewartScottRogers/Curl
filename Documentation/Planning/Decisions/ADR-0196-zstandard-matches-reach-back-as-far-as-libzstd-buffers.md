# ADR-0196 — Zstandard matches reach back as far as libzstd's streaming decoder buffers, and every sequences error is `CorruptionDetected`

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-860.
Refines ADR-0185's "Features" for the sequences section, and ADR-0192's temporary
`SequencesNotYetSupported`.

## Context

BL-860 taught `ZstandardDecoder` (`Curl.Zstandard.UnitLibrary`) to decode and execute a
compressed block's sequences section. Two behaviours had to be settled:

- **How far back a match may reach.** RFC 8878 section 3.1.1.1.2 makes `Window_Size` the
  memory a decoder needs, and a conforming encoder never emits an offset beyond it, but the
  RFC does not say what a decoder does with one. libzstd's streaming decoder
  (`ZSTD_decompressStream`), the one curl drives, checks an offset only against the bytes
  it still buffers, and its buffer holds `Window_Size` plus two `Block_Maximum_Size` plus
  64 bytes (`2 * WILDCOPY_OVERLENGTH`). Measured with `Record-CurlExchange.ps1`: curl
  8.21.0 with libzstd 1.5.7, given a frame with a 1 KiB window, three raw blocks of 1024
  bytes and a match 3040 bytes back, prints the right bytes and exits 0; a match 1025 bytes
  back after 2000 bytes does too. Beyond what it buffers, libzstd either fails
  (`corruption_detected`, when the offset passes all it holds) or, once its ring buffer
  has wrapped, copies stale bytes and exits 0 (a match 3071 bytes back in the same frame
  prints bytes the frame never held).
- **Which error each malformed sequences section is.** libzstd reports every one - a
  reserved `Symbol_Compression_Modes` bit, an RLE code above the code's maximum, an FSE
  table over its `Accuracy_Log` limit or cut short, `Repeat_Mode` with no earlier table in
  the frame, a bitstream without its padding bit, bits left over or read past its start,
  a sequence taking more literals than there are, an offset of 0 or reaching before the
  frame's content, a block regenerating more than `Block_Maximum_Size`, content passing
  `Frame_Content_Size` - as `corruption_detected`, and curl fails each with exit 61
  (measured for every malformed frame in `ZstandardSequencesDecoderTests`).

## Decision

- The decoder keeps a frame's latest `Window_Size + 2 * Block_Maximum_Size + 64` bytes
  (`ZstandardHistory`, a ring buffer that grows to that as content arrives), and a match
  may copy from anywhere in them. A match reaching further back than the frame's content
  so far, or than the history holds, is `CorruptionDetected`.
- Every malformed sequences section is `CorruptionDetected`; `SequencesNotYetSupported`
  is removed, as ADR-0192 said it would be.
- Tables for `Repeat_Mode` and the three repeat offsets (1, 4, 8) carry from block to
  block within a frame and are reset when a new frame starts; an `RLE_Mode` or
  `Predefined_Mode` table is repeatable like an FSE-compressed one, as in libzstd.

## Consequences

- Every frame a conforming encoder writes decodes as in libzstd, and so does every frame
  whose matches overreach the window only as far as libzstd still buffers. Up to the first
  time libzstd's ring buffer wraps, Curl and curl agree on every offset.
- After libzstd's buffer wraps, a malformed frame whose offset overreaches can make curl
  print stale bytes where Curl exits 61. Matching that would mean reproducing libzstd's
  buffer layout and its wild copies' overwrites byte for byte, for frames no encoder
  writes; not done.
- History costs at most 256 KiB and 64 bytes more than the window, and it grows from 64 KiB
  as content arrives, so a small frame with a large window allocates little.

## Alternatives considered

- **Refuse any offset beyond `Window_Size`, as a strict reading of RFC 8878 suggests.**
  curl decodes such a frame (measured: the 1025-byte match in a 1 KiB window), so Curl
  would exit 61 where curl exits 0. Not a drop-in replacement. Rejected.
- **Keep only `Window_Size` plus one block.** Simpler to state, but still refuses matches
  curl decodes correctly (measured: 3040 bytes back in a 1 KiB window). Rejected.
