# ADR-0430: A TFTP download receives the block size plus four bytes, as curl does

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1668
- Decided by Claude under Stewart's delegation.

## Context

`TftpDownload` received every datagram into a 65468-byte buffer, so a DATA packet longer
than the agreed block size plus its four-byte header was written whole. curl 8.21.0's
`tftp_receive_packet` passes `blksize + 4` to `recvfrom`, where `blksize` is 512 until an
OACK agrees another. What happens to a longer datagram is the socket's business:

- Windows (measured, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Tftp` with
  `RRQ=PACKET` sending DATA 1 of 600 payload bytes before a DATA 1 of one byte):
  `recvfrom` fails with WSAEMSGSIZE, curl notes `Received too short packet`, writes and
  acknowledges nothing of it, and the next DATA 1 completes the download with exit 0.
- Linux and macOS: `recvfrom` succeeds with the datagram cut to the buffer, so curl writes
  the first 512 payload bytes and acknowledges block 1. Not measured in the lane: WSL's
  curl could not reach the Windows loopback recorder. This follows from the documented
  `recvfrom` semantics for a short buffer and curl's own code path.

## Decision

`TftpDownload` passes only the first `blockSize + 4` bytes of its buffer to each receive.
The UDP channel stays a thin socket adapter: .NET's `ReceiveFromAsync` already behaves as
each platform's `recvfrom` does (truncates on Unix, throws `SocketError.MessageSize` on
Windows). `TftpTimeLimits.ReceiveBeforeAsync` answers `MessageSize` as it answers a refused
receive, which the transfer treats as a datagram under four bytes.

## Consequences

Each platform's answer is pinned against a fake channel that behaves as that platform's
socket, so both tests run everywhere. A Linux run of the recorder can confirm the
unmeasured answer later.
