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
completed:
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

- [ ] A named test asserts the exact WRQ bytes above for a 3-byte `Upload` of `abc` to
      `tftp://h/dest.txt`, then DATA 1 `abc` sent to the endpoint the ACK 0 came from,
      and exit 0 with nothing written to `Output`.
- [ ] A test uploads 1100 bytes and asserts DATA blocks 1-3 of 512, 512 and 76 bytes,
      each sent only after the previous block's ACK.
- [ ] A test asserts an ERROR packet in reply to the WRQ returns the exit code and
      message BL-045's table gives (code 6: exit 73 `Remote file already exists`).
- [ ] The three unmeasured cases in `Context` are measured against curl 8.21.0, recorded
      in `Notes`, and pinned by named tests.
- [ ] Every BL-045 test still passes; no test is tagged `Integration`.
- [ ] `dotnet build Curl.Protocol.Tftp.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
