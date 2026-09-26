# Curl.Protocol.Tftp.UnitLibrary

Phase 4.

Trivial File Transfer Protocol. The only UDP protocol in the set.

**URL schemes:** `tftp`

This library may reference `Curl.Protocol.Abstractions.UnitLibrary` and nothing
else horizontal. Referencing another protocol library is a build break, and
`Curl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `SslStream` or `HttpClient` here. The seam is
`IDatagramConnector`, which opens an `IDatagramChannel` (ADR-0005), not `IConnection`:
TFTP needs datagram boundaries and the server's reply port (its transfer identifier),
which a byte stream cannot express. The tests in the matching `.UnitTests` project drive
`TftpProtocolHandler` through a scripted fake channel with no network.

## What is implemented

- Download with curl 8.21.0's default read request (`tsize 0`, `blksize 512`,
  `timeout 6`), OACK handling (ACK 0, then the acknowledged `blksize`), ACKs sent to
  the endpoint the DATA came from, and every TFTP ERROR code mapped to curl's exit code
  and message (`TftpErrorMapping`).
- Upload (`-T`) with curl 8.21.0's default write request (`tsize` = the upload's
  length, or 0 when it cannot seek; `blksize 512`, `timeout 6`), each DATA block sent
  after the previous block's ACK to the endpoint that ACK came from, an OACK taken as
  ACK 0 with its `blksize`, and a zero-length last block when the length is an exact
  multiple of the block size (so an empty upload is one empty DATA 1).
- Not yet: `--tftp-blksize`, `--tftp-no-options`, retransmission, and
  the timeout that ends a silent transfer. Until then a silent server leaves the
  receive waiting until the transfer's token is cancelled.
