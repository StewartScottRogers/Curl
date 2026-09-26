---
id: BL-045
title: Implement TFTP download with curl's default option negotiation
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-033, BL-034, BL-035]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-045 — Implement TFTP download with curl's default option negotiation

## Goal

`TftpProtocolHandler` in `Curl.Protocol.Tftp.UnitLibrary` downloads a `tftp://` file over
an `IDatagramChannel`, sending curl 8.21.0's read request with its default options,
following an option acknowledgement, and mapping every TFTP error packet to curl's exit
code and message.

## Context

Requirements: the `tftp://` rows BL-033 adds to `Documentation/Product/Requirements.md`.
Seams: ADR-0005 (`IDatagramConnector`, `IDatagramChannel`, `DatagramReceived`) and
ADR-0006 (`TransferContext`). Protocol: RFC 1350, with options per RFC 2347 (OACK),
RFC 2348 (`blksize`) and RFC 2349 (`timeout`, `tsize`). Default port 69
(<https://curl.se/docs/url-syntax.html>). Exit codes:
<https://curl.se/libcurl/c/libcurl-errors.html>.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24) against a
loopback UDP server that answered from a second port:

- `tftp://h/file.txt` sent the RRQ
  `00 01 "file.txt" 00 "octet" 00 "tsize" 00 "0" 00 "blksize" 00 "512" 00 "timeout" 00 "6" 00`.
  The server answered `00 03 00 01 "hello"` (DATA block 1, 5 bytes) from its new port;
  curl sent `00 04 00 01` (ACK 1) to that port, wrote `hello`, exit 0 - a block shorter
  than the block size ends the transfer.
- With the server answering `00 06 "blksize" 00 "1024" 00` (OACK), curl sent ACK 0, then
  ACK 1 after the DATA block.
- ERROR packets (`00 05 <code> <message> 00`) gave:

| TFTP error code | Exit | Message |
| --- | --- | --- |
| 0 | 71 `TftpIllegal` | `TFTP: Illegal operation` |
| 1 | 68 `TftpNotFound` | `TFTP: File Not Found` |
| 2 | 69 `TftpPerm` | `TFTP: Access Violation` |
| 3 | 70 `RemoteDiskFull` | `Disk full or allocation exceeded` |
| 4 | 71 `TftpIllegal` | `TFTP: Illegal operation` |
| 5 | 72 `TftpUnknownId` | `TFTP: Unknown transfer ID` |
| 6 | 73 `RemoteFileExists` | `Remote file already exists` |
| 7 | 74 `TftpNoSuchUser` | `TFTP: No such user` |
| 8 | 42 `AbortedByCallback` | `Operation was aborted by an application callback` |

- `tftp://h/` (no file name): exit 71, `Missing filename`, and no packet was sent.

`Curl.Protocol.Tftp.UnitLibrary/CLAUDE.md` tells the handler to take `IConnection`, which
ADR-0005 replaces with `IDatagramConnector` for TFTP; this task corrects it.

Upload (`-T`), `--tftp-blksize` and `--tftp-no-options` are the next two tasks.
Retransmission after a lost packet and the timeout that ends a silent transfer are not
measured yet: measure them with curl 8.21.0 during this run and file them as a task
rather than guessing.

## Acceptance criteria

- [ ] `TftpProtocolHandler(IDatagramConnector connector)` implements `IProtocolHandler`
      with `SupportedSchemes` exactly `["tftp"]`, opens port 69 unless the URL names
      one, and constructs no `Socket`.
- [ ] A named test asserts the exact RRQ bytes above for `tftp://h/file.txt`, sent to
      `ServerEndPoint`, using a scripted fake `IDatagramChannel` declared in
      `Curl.Protocol.Tftp.UnitTests`.
- [ ] A test asserts the ACK goes to the endpoint the DATA came from, not to
      `ServerEndPoint`, and that `hello` reaches `Output` with exit 0 and
      `BytesTransferred` 5.
- [ ] A test with three blocks (512, 512, 100 bytes) asserts ACKs 1, 2 and 3 and all
      1124 bytes in order; a test with an OACK asserts ACK 0 is sent first and the
      acknowledged block size is then used to decide the last block.
- [ ] One `[DataRow]` per row of the error table asserts the `CurlExitCode` and
      `ErrorMessage` exactly.
- [ ] A test asserts `tftp://h/` returns exit 71 `Missing filename` without opening the
      connector.
- [ ] A failed `DatagramOpenResult` is returned with its code and message unchanged.
- [ ] Retransmission and timeout behaviour are measured, recorded in `Notes`, and filed
      as a task whose ID is in `Notes`.
- [ ] `Curl.Protocol.Tftp.UnitLibrary/CLAUDE.md` names `IDatagramConnector` (ADR-0005) as
      the seam.
- [ ] Every test builds its context with `TransferContext`; no `ITransferContext`
      implementation is declared in the test project, and no test is tagged
      `Integration`.
- [ ] `dotnet build Curl.Protocol.Tftp.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Protocol.Tftp.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

Do not edit `Documentation/Product/Requirements.md`; if a tftp row there is wrong, file a
task.

## Log

- 2026-09-26: Created.
