---
id: BL-971
title: Report file:// download data in curl's 102399-byte read chunks
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-936]
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-971 — Report file:// download data in curl's 102399-byte read chunks

## Goal

A `file://` download larger than 16384 bytes reports its received data (`--trace`'s `<= Recv data`) in the same chunks curl 8.21.0 does: 102399 bytes at a time, the last one shorter.

## Context

- Measured in BL-936 (curl 8.21.0, Schannel build, Windows): `curl -s --trace-ascii tr.txt -o out file:///<dir>/big.txt` on a 1000000-byte file traces nine `<= Recv data, 102399 bytes (0x18fff)` blocks and one `<= Recv data, 78409 bytes (0x13249)`.
- BL-936 reports `ITransferEvents.ReportDataReceived` once per read in `FileProtocolHandler.CopyAsync`, and a download reads `ChunkSize` = 16384 bytes at a time, so a file past 16384 bytes is traced in 16384-byte blocks.
- The 16384 is still right for the *writes* to the output (libcurl hands the write callback at most `CURL_MAX_WRITE_SIZE`): existing tests pin write lengths of 16384 and the exit 23 `passed 16384` message. So the fix is to read 102399 bytes, report them as one chunk, then write them in 16384-byte slices, keeping `--max-filesize` and the write-failure messages as they are.
- `FileProtocolHandlerTests.ExecuteAsync_SourceReadFailsMidBody_EndsTheBodyThereAndSucceeds` pins 16384 bytes delivered before a failing second read; with 102399-byte reads that becomes 102399 (or the file's length) - re-measure with a byte-range-locked source before changing it.

## Acceptance criteria

- [ ] A test in `Curl.Protocol.File.UnitTests` pins, through a recording `ITransferEvents`, received-data chunks of 102399, 102399, … and the remainder for a file over 204798 bytes.
- [ ] The output still receives writes of at most 16384 bytes; the existing write-length, `--max-filesize` and exit 23 message tests pass.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.File.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
