---
id: BL-046
title: Implement TFTP upload with -T
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-045]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-046 — Implement TFTP upload with -T

## Goal

The TFTP handler uploads `ITransferContext.Upload` (`-T`) with curl 8.21.0's write
request and DATA sequence.

## Context

Builds on the TFTP download handler from BL-045, in the same library; the error-packet
mapping it pins applies unchanged to uploads. Protocol: RFC 1350 (WRQ, DATA, ACK) and
RFC 2349 (`tsize`). Upstream: "curl can do TFTP downloads and uploads"
(<https://curl.se/docs/manpage.html>, checked 2026-09-26).

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24):
`curl -T up.txt tftp://127.0.0.1:16970/dest.txt` with `up.txt` holding `abc` sent the WRQ
`00 02 "dest.txt" 00 "octet" 00 "tsize" 00 "3" 00 "blksize" 00 "512" 00 "timeout" 00 "6" 00`;
after the server's `00 04 00 00` (ACK 0) from its new port, curl sent
`00 03 00 01 "abc"` (DATA 1) to that port; after ACK 1, exit 0 with nothing on stdout.

Not yet measured, so measure during the run rather than guess: an upload whose length is
an exact multiple of the block size (RFC 1350 requires a final zero-length DATA block),
the `tsize` sent when `Upload` is not seekable and its length is unknown, and an empty
upload.

## Acceptance criteria

- [x] A named test asserts the exact WRQ bytes above for a 3-byte `Upload` of `abc` to
      `tftp://h/dest.txt`, then DATA 1 `abc` sent to the endpoint the ACK 0 came from,
      and exit 0 with nothing written to `Output`.
- [x] A test uploads 1100 bytes and asserts DATA blocks 1-3 of 512, 512 and 76 bytes,
      each sent only after the previous block's ACK.
- [x] A test asserts an ERROR packet in reply to the WRQ returns the exit code and
      message BL-045's table gives (code 6: exit 73 `Remote file already exists`).
- [x] The three unmeasured cases in `Context` are measured against curl 8.21.0, recorded
      in `Notes`, and pinned by named tests.
- [x] Every BL-045 test still passes; no test is tagged `Integration`.
- [x] `dotnet build Curl.Protocol.Tftp.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

- Measured on 2026-09-26 with curl 8.21.0 against a loopback Python UDP server that
  answered from a second port:
  - `-T` of 512 bytes: WRQ `tsize 512`; DATA 1 of 512 bytes, then a zero-length DATA 2;
    exit 0 after ACK 2. Pinned by `ExecuteAsync_UploadExactly512Bytes_SendsTsize512AndEndsWithZeroLengthData2`.
  - `printf abc | curl -T -` (not seekable): WRQ `tsize 0`, then DATA 1 `abc`. Pinned by
    `ExecuteAsync_UploadCannotSeek_SendsTsizeZero`.
  - `-T` of an empty file: WRQ `tsize 0`, one zero-length DATA 1, exit 0 after ACK 1.
    Pinned by `ExecuteAsync_EmptyUpload_SendsTsizeZeroAndOneEmptyData1`.
  - Also measured: a server that answers the WRQ with OACK `blksize 8` gets DATA 1 of
    8 bytes straight away (the OACK stands in for ACK 0). Pinned by
    `ExecuteAsync_OptionAcknowledgementOfWriteRequest_SendsData1InTheAcknowledgedBlockSize`.
- Choices: `tsize` is `Upload.Length - Upload.Position` when the stream can seek, else 0
  (curl sends 0 when the size is unknown). The upload succeeds when the last block's ACK
  arrives; `BytesTransferred` is the bytes uploaded. An OACK after DATA 1 has gone, an
  ACK for any block but the one last sent, a datagram under 4 bytes and other opcodes
  are ignored, mirroring the download's tolerance. Each block is filled from as many
  reads as it takes, so a pipe that delivers in small pieces still sends full blocks.
- New `TftpUpload` beside `TftpDownload`; `TftpPackets` gained `BuildWriteRequest` and
  `BuildData` (read and write requests share one builder). Library coverage stays 100%
  line and branch. Retransmission is still BL-075.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. tftp:// uploads -T with curl 8.21.0's WRQ, DATA sequence, zero-length last block and tsize
