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
completed:
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

- [ ] A test in `Curl.Protocol.File.UnitTests` uploads a 100000-byte seekable source whose
      second read fails and asserts `ErrorMessage` is
      `client read function EOF fail, only 65536/100000 of needed bytes read`.
- [ ] Existing download tests asserting 16384-byte writes still pass unchanged.
- [ ] The `UploadSourceReadFailed` remarks no longer describe the chunk-size mismatch.
- [ ] `dotnet test Curl.Protocol.File.UnitTests --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
