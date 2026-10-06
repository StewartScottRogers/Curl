---
id: BL-785
title: Decide where the hand-built Zstandard decoder lives and file its tasks
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-785 — Decide where the hand-built Zstandard decoder lives and file its tasks

## Goal

An ADR decides which library holds a hand-built Zstandard (RFC 8878) decoder used by both HTTP `--compressed` and TLS certificate decompression (RFC 8879), amends ADR-0120's table if it is a new hand-built library, and the tasks that create and build it are filed.

## Context

- ADR-0140 (BL-695): OpenSSL 3.5.5's ClientHello offers `compress_certificate` with zlib, brotli and zstd; the BCL has zlib (`ZLibStream`) and Brotli (`BrotliDecoder`) but no Zstandard. ADR-0020 keeps `--compressed` at `deflate, gzip, br` "until a zstd decoder exists", while curl 8.21.0's mingw build advertises zstd.
- Two consumers in different layers: `Curl.Protocol.Http.UnitLibrary` (content decoding) and `Curl.Tls.UnitLibrary` (BL-786). Protocol libraries may reference only Abstractions and ADR-0120's hand-built libraries, so a shared decoder is a new hand-built library row (for example `Curl.Zstandard.UnitLibrary`, referencing nothing) or an addition to an existing row; the ADR decides and says why.
- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): hand-built, own `UnitLibrary` and `UnitTests`, never a package.

## Acceptance criteria

- [x] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists, Status Accepted, marked "Decided by Claude under Stewart's delegation", naming the library, what it may reference, its API shape (streaming decode, frame and window limits) and its test vectors (RFC 8878 and the reference implementation's test corpus, cited).
- [x] If the ADR adds a hand-built library, it amends ADR-0120's table and files the `ProtocolIsolationTests` change with the project-creation task.
- [x] Tasks are filed on the board for creating the library (if new), building the decoder, and advertising `zstd` in `--compressed` (superseding ADR-0020's limit), and BL-786 is left depending on the decoder task.
- [x] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

- Decision (ADR-0185): new hand-built library `Curl.Zstandard.UnitLibrary`, referencing
  nothing, holding the decoder and XXH64. Not `Curl.Cryptography` (compression is not
  cryptography, so the name would lie), not `Curl.Tls` (HTTP would compile against TLS),
  not `Curl.Protocol.Http` (TLS may not reference a protocol), not a broader
  `Curl.Compression` (the BCL already has zlib and Brotli, so it would promise more than
  it holds).
- API mirrors the BCL's `BrotliDecoder` (push `Decompress` with `OperationStatus`, `Done`
  at each frame end) because `HttpContentCodingDecoder` already drives Brotli that way to
  find the end of the coded stream; `TryDecompress` into a caller-sized buffer suits
  RFC 8879's declared `uncompressed_length`. Default window limit 2^27 is libzstd's
  `ZSTD_WINDOWLOG_LIMIT_DEFAULT`, which curl leaves in place.
- ADR-0120 amended in place (its own "Adding a hand-built library" section says a new
  library joins by amending it): new row, `Curl.Tls` row widened, amendment note at the top.
- Filed: BL-857 (create projects + `ProtocolIsolationTests` row), BL-858 (frames,
  raw/RLE blocks, skippable frames, XXH64), BL-859 (literals, Huffman, FSE), BL-860
  (sequences, golden corpus), BL-861 (HTTP `Content-Encoding: zstd`, advertise `zstd`,
  supersede ADR-0020). The decoder is split in three because a full compressed-block
  decoder is too large for one run. BL-786 now depends on BL-860.
- Filing tasks and editing BL-786 changed `Tasks/Backlog`, outside this task's
  `touches`; that is the board script's own output, which every task filing produces, and
  no task in `Doing` names those files.
- No `.cs` or project file changed; `dotnet build` clean and fast tests green anyway.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ADR-0185 places the hand-built Zstandard decoder in Curl.Zstandard.UnitLibrary (amends ADR-0120); BL-857 to BL-861 filed and BL-786 waits on BL-860
