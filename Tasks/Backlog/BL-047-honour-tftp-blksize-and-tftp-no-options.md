---
id: BL-047
title: Honour --tftp-blksize and --tftp-no-options
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-046]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-047 — Honour --tftp-blksize and --tftp-no-options

## Goal

The TFTP handler honours `ITransferContext.TftpBlockSize` (`--tftp-blksize`), clamped to
8-65464, and `ITransferContext.TftpNoOptions` (`--tftp-no-options`), in both directions,
as curl 8.21.0 does.

## Context

Builds on the TFTP download and upload tasks (BL-045, BL-046). Upstream: "The default of
512 bytes is used if this option is not specified", "Valid range as per RFC 2348 is
8-65464 bytes", and "If the server does not return an option acknowledgment or returns an
option acknowledgment with no block size, the default of 512 bytes is used"
(<https://curl.se/libcurl/c/CURLOPT_TFTP_BLKSIZE.html>); `--tftp-no-options` sends none of
the RFC 2347, 2348 and 2349 options
(<https://curl.se/libcurl/c/CURLOPT_TFTP_NO_OPTIONS.html>). Both pages checked on
2026-09-26.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24):

- `--tftp-blksize 1024`: the RRQ carried `blksize 00 "1024"`; the server's OACK
  `blksize 1024` was answered with ACK 0.
- `--tftp-blksize 5` sent `blksize 00 "8"`; `--tftp-blksize 70000` sent
  `blksize 00 "65464"`; `--tftp-blksize 8` sent `"8"`.
- `--tftp-no-options`: the RRQ was exactly `00 01 "file.txt" 00 "octet" 00`, and a plain
  DATA reply was acknowledged and written as usual.

Not yet measured: a block size requested but the server answering with plain DATA (no
OACK) - the upstream text says 512 is then used - and `--tftp-no-options` on an upload.
Measure both during the run.

## Acceptance criteria

- [ ] `[DataRow]` tests assert the `blksize` value sent for `TftpBlockSize` 5, 8, 1024,
      65464 and 70000 is 8, 8, 1024, 65464 and 65464, with `tsize` and `timeout` unchanged
      from BL-045's default request.
- [ ] A test asserts `TftpNoOptions` sends exactly `00 01 "file.txt" 00 "octet" 00` and
      completes a download from a plain DATA reply.
- [ ] A test asserts that with `TftpBlockSize` 1024 and an OACK of 1024, a 1024-byte
      block is not taken as the last one, and a 1000-byte block is.
- [ ] The two unmeasured cases in `Context` are measured against curl 8.21.0, recorded in
      `Notes`, and pinned by named tests.
- [ ] Every BL-045 and BL-046 test still passes; no test is tagged `Integration`.
- [ ] `dotnet build Curl.Protocol.Tftp.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

## Log

- 2026-09-26: Created.
