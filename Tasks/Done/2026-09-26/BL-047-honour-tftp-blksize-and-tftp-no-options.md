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
completed: 2026-09-26
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

- [x] `[DataRow]` tests assert the `blksize` value sent for `TftpBlockSize` 5, 8, 1024,
      65464 and 70000 is 8, 8, 1024, 65464 and 65464, with `tsize` and `timeout` unchanged
      from BL-045's default request.
- [x] A test asserts `TftpNoOptions` sends exactly `00 01 "file.txt" 00 "octet" 00` and
      completes a download from a plain DATA reply.
- [x] A test asserts that with `TftpBlockSize` 1024 and an OACK of 1024, a 1024-byte
      block is not taken as the last one, and a 1000-byte block is.
- [x] The two unmeasured cases in `Context` are measured against curl 8.21.0, recorded in
      `Notes`, and pinned by named tests.
- [x] Every BL-045 and BL-046 test still passes; no test is tagged `Integration`.
- [x] `dotnet build Curl.Protocol.Tftp.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

- Plan: `TftpPackets.RequestedBlockSize(context)` gives the `blksize` to ask for (clamped
  8-65464) or `null` for `--tftp-no-options`; `BuildReadRequest`/`BuildWriteRequest` take
  it and send `file` and `octet` alone when it is `null`. The block size in force stays
  512 until an OACK grants another, which is what the download and upload already did.
  Tests are in `Curl.Protocol.Tftp.UnitTests/TftpOptionsTests.cs`.
- Measured with curl 8.21.0 on 2026-09-26 against a loopback Python UDP server:
  - `--tftp-blksize 1024`, answered with plain 512-byte DATA 1 and 100-byte DATA 2 (no
    OACK): both acknowledged, 612 bytes written, exit 0. So 512 is used.
    Pinned by `ExecuteAsync_BlockSize1024AnsweredWithPlainData_UsesTheDefault512`.
  - `--tftp-no-options -T` (3 bytes): WRQ exactly `00 02 "dest.txt" 00 "octet" 00`,
    DATA 1 of 3 bytes after ACK 0, exit 0.
    Pinned by `ExecuteAsync_TftpNoOptionsUpload_SendsFileNameAndOctetOnlyThenData`.
  - `--tftp-no-options --tftp-blksize 1024`: no options sent either.
  - `--tftp-blksize 0` sends `blksize 512`, so 0 is treated as "not given" (choice: follow
    the measurement rather than clamp 0 to 8). `--tftp-blksize -1` is refused by curl's
    command line with exit 2, so a negative never reaches the handler; it would clamp to 8.
  - Not in scope, seen in passing: with `--tftp-blksize 1024` and a plain 1024-byte DATA 1
    (no OACK) curl sent no ACK at all and timed out; this handler would accept the block.
    Left for the retransmission/timeout work.
- 100% line and branch coverage of `Curl.Protocol.Tftp.UnitLibrary`; 51 tests.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. --tftp-blksize (clamped 8-65464, 0 = 512) and --tftp-no-options work for TFTP download and upload
