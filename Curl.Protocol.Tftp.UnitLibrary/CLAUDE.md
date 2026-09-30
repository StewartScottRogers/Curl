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
  length, or 0 when it cannot seek; `blksize 512`, `timeout` as for a download), each
  DATA block sent after the previous block's ACK to the endpoint that ACK came from, an
  OACK taken as ACK 0 with its `blksize`, and a zero-length last block when the length
  is an exact multiple of the block size (so an empty upload is one empty DATA 1).
- `--tftp-blksize` (`TftpBlockSize`), clamped to 8-65464 and sent as `blksize`; 0 or
  absent sends 512. The block size in force is 512 until an OACK grants another, so a
  server that answers with plain DATA is read in 512-byte blocks, as curl does.
- `--tftp-no-options` (`TftpNoOptions`): the read or write request is the file name
  and `octet` alone, with no `tsize`, `blksize` or `timeout`.
- Retransmission and timeouts (`TftpDownload`, `TftpUpload`, `TftpRetrySchedule`,
  `TftpTimeLimits`), as curl 8.21.0's `tftp_set_timeouts` derives them: from the time
  left (`ConnectTimeout`, 300 s by default, or `MaxTime` if sooner; after the first
  DATA/ACK/OACK, what `MaxTime` leaves, else 15 s) come `retry_max` =
  clamp(seconds / 5, 3, 50) and `retry_time` = max(1, seconds / retry_max). The request's
  `timeout` option is `retry_time`, and the last packet is re-sent every
  `retry_time + 1` s. An unanswered RRQ or WRQ (sent `retry_max` times) ends with exit 7
  `Could not connect to server`; an ACK or DATA block re-sent `retry_max` times without
  an answer ends with exit 28 `Timeout was reached`; `MaxTime` passing ends with exit 28
  `Operation timed out after N milliseconds with M bytes received` (M is 0 for an
  upload). The first datagram pins the server's endpoint; one from anywhere else ends the
  transfer with exit 56 `Data received from another address` and the stranger is sent
  nothing. Every wait goes through `ITransferContext.TimeProvider`.
- Download only: a repeated last block is re-ACKed and not written twice.
- Download and upload: a datagram under four bytes re-sends the last packet (RRQ, WRQ,
  ACK or DATA) at once and counts a retry without moving the next scheduled re-send;
  whatever failure then ends the transfer says `Received too short packet` (curl keeps
  the first failure it noted; measured for a download at exits 7, 28, 56 and 68).
- Upload only: an ACK of the wrong block re-sends the last packet at once and counts a
  retry (one too many is exit 55 `tftp_tx: giving up waiting for block N ack`), without
  moving the next scheduled re-send. Before DATA 1, a re-send after an ACK is the WRQ's
  first four bytes, as curl sends.
- Upload only: an OACK that arrives after DATA 1 is taken as curl 8.21.0 takes it: its
  `blksize` comes into force and the block count restarts, so the next DATA is block 1
  again, carrying the next bytes of the upload (nothing is re-read) at the new size.
  An OACK after the last block has gone sends an empty DATA 1.
- Through an HTTP or HTTPS proxy (`ITransferContext.Proxy` of kind `Http`, `Http10` or
  `Https`), no datagram is sent: the optional `IConnector` connects to the proxy (TLS for
  `Https`, `IsForwardProxy` set), the MASQUE `connect-udp` request curl 8.21.0's Schannel
  build sends is written to it (`TftpMasqueRequest`; `https://` in the request line for an
  HTTPS proxy), and the proxy's reply header block decides the failure
  (`TftpMasqueReply`): `101` or `2xx` is exit 7 `bind() failed; Invalid arguments`, any
  other status exit 7 `CONNECT-UDP tunnel failed, response N` (0 for a first line that is
  not a status line), a close before the header block ends exit 56
  `Proxy CONNECT aborted`. All of it happens before the file name is checked (ADR-0056,
  rule 4; ADR-0096; measured by BL-330, BL-345 and BL-398). Without that connector the
  request is not sent and the result is exit 7 `bind() failed; Invalid arguments`.
- Through a SOCKS proxy (`Socks4`, `Socks4a`, `Socks5`, `Socks5Hostname`) nothing is sent,
  neither to the proxy nor to the server, and the transfer ends with exit 97
  `Send failure: Socket is not connected`, before the file name is checked (ADR-0096;
  measured by BL-398).
- Diagnostic log (`--log-level`, ADR-0222, BL-927): `TftpTransferLog` writes under the
  `tftp` component - a download's read request and its options and the options an OACK
  agreed (`info`), each packet received (`verbose`), a retransmission and a `blksize` the
  server changed or ignored (`warning`), an ERROR packet's code and text (`error`) - and,
  for every transfer, its end: bytes and milliseconds at `info`, or its `CurlExitCode`
  at `error`.
- `-v` and `--trace` (BL-933): `TftpTransferEvents` reports curl 8.21.0's lines to
  `ITransferContext.Events` once the channel opens - `  Trying <ip>:<port>...`,
  `Established connection to <host> (<ip> port <port>) from  port 0 `, `set timeouts for
  state 0|1|2; Total <ms left, 0 for none>, retry R maxtry M`, each OACK option as
  `got option=(n) value=(v)` with `blksize parsed from OACK (A) requested (R)` and, for a
  download only, `tsize parsed from OACK (N)`, `Connected for receive|transmit`, `Timeout
  waiting for block N ACK. Retries = R` for a re-send once the server has answered,
  `TFTP error: <text>` for an ERROR packet whose text ends in a NUL, and `shutting down
  connection #0` - and each downloaded block's bytes as data received. An upload reports
  no data sent, as curl reports none.
