---
id: BL-105
title: Read file:// uploads in 65536-byte chunks so the exit 26 byte count matches curl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-023]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-105 — Read file:// uploads in 65536-byte chunks so the exit 26 byte count matches curl

## Goal

An upload through `FileProtocolHandler` reads its source 65536 bytes at a time, so the
`client read function EOF fail, only <read>/<needed> of needed bytes read` message reports
the same `<read>` curl 8.21.0 does for the same failure point.

## Context

BL-023 measured curl 8.21.0 on Windows: a 100000-byte `-T` source locked from byte 99000
prints `only 65536/100000`, because curl reads an upload 64 KiB at a time. The handler reads
every body in `ChunkSize` = 16384 bytes (curl's `CURL_MAX_WRITE_SIZE`, which is right for a
download), so for the same failure it would report `only 98304/100000`. See the remarks on
`FileTransferMessages.UploadSourceReadFailed` and the Notes of BL-023. The download chunk size
is measured and must stay 16384; `CrlfUploadConverter` is constructed with the chunk size and
must be given the upload one.

## Acceptance criteria

- [x] A test in `Curl.Protocol.File.UnitTests` uploads a 100000-byte seekable source whose
      second read fails and asserts `ErrorMessage` is
      `client read function EOF fail, only 65536/100000 of needed bytes read`.
- [x] Existing download tests asserting 16384-byte writes still pass unchanged.
- [x] The `UploadSourceReadFailed` remarks no longer describe the chunk-size mismatch.
- [x] `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

- Delivered directly rather than through the full `/feature` agent chain: the change is one
  constant, one parameter and a rounding in one method, and the tests were written against
  measured curl output first.
- Measured curl 8.21.0 (mingw64, Windows) on 2026-09-26 with a 200000-byte source locked from
  byte 150000: `-C 0`, `-C 10`, `-C 70000`, `-C 131072` and `-C 140000` all print
  `only 131072/200000`, with `size_upload` 131072, 131062, 61072, 0 and 0. So curl reads the
  source in 65536-byte chunks from the start of the file, skipped bytes included, and the
  number it reports is the chunk boundary the failed read began at. BL-023's `-C 10` row
  (`only 65536/100000`, size_upload 65526) says the same thing.
- Choice: the handler still seeks past a `-C` skip (the existing, tested behaviour), then
  sizes its first read to reach the next 65536-byte boundary and rounds the reported count
  down to one. That reproduces every measured case. It differs from curl only when a read
  inside the skipped range itself would fail, which a seek never sees; not filed, as it
  needs a lock inside the skipped bytes to observe.
- Upload tests that assumed 16384-byte upload chunks now use a 100000-byte `UploadContent()`
  and `UploadChunkSize`; the `-C 10` test now asserts the measured `65536/100000` rather
  than the unmeasured `16394/40000`. Download tests are unchanged. Non-seekable skips also
  read in 65536-byte chunks; that is not observable.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. file:// uploads read in 65536-byte chunks aligned to the file start, so exit 26 reports the same count as curl 8.21.0, -C included
