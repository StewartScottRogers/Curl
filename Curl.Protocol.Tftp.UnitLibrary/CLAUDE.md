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
  `timeout 6` when no time limit is given), OACK handling (ACK 0, then the acknowledged `blksize`), ACKs sent to
  the endpoint the DATA came from, and every TFTP ERROR code mapped to curl's exit code
  and message (`TftpErrorMapping`).
- Upload (`-T`) with curl 8.21.0's default write request (`tsize` = the upload's
  length, or 0 when it cannot seek; `blksize 512`, `timeout 6`), each DATA block sent
  after the previous block's ACK to the endpoint that ACK came from, an OACK taken as
  ACK 0 with its `blksize`, and a zero-length last block when the length is an exact
  multiple of the block size (so an empty upload is one empty DATA 1).
- `--tftp-blksize` (`TftpBlockSize`), clamped to 8-65464 and sent as `blksize`; 0 or
  absent sends 512. The block size in force is 512 until an OACK grants another, so a
  server that answers with plain DATA is read in 512-byte blocks, as curl does.
- `--tftp-no-options` (`TftpNoOptions`): the read or write request is the file name
  and `octet` alone, with no `tsize`, `blksize` or `timeout`.
- Download retransmission and timeouts (`TftpDownload`, `TftpRetrySchedule`), as curl
  8.21.0's `tftp_set_timeouts` derives them: from the time left (`ConnectTimeout`, 300 s
  by default, or `MaxTime` if sooner; after the first DATA/OACK, what `MaxTime` leaves,
  else 15 s) come `retry_max` = clamp(seconds / 5, 3, 50) and `retry_time` =
  max(1, seconds / retry_max). The RRQ's `timeout` option is `retry_time`, and the last
  packet is re-sent every `retry_time + 1` s. An unanswered RRQ (sent `retry_max` times)
  ends with exit 7 `Could not connect to server`; an ACK re-sent `retry_max` times
  without an answer ends with exit 28 `Timeout was reached`; `MaxTime` passing ends
  with exit 28 `Operation timed out after N milliseconds with M bytes received`. A
  repeated last block is re-ACKed and not written twice. The first datagram pins the
  server's endpoint; one from anywhere else ends the transfer with exit 56
  `Data received from another address` and the stranger is sent nothing. Every wait
  goes through `ITransferContext.TimeProvider`.
- Not yet: retransmission and timeouts for an upload; its write request still carries
  `timeout 6` whatever the time limits, and a silent server leaves it waiting until the
  transfer's token is cancelled.
