# ADR-0936: Record-CurlExchange.sh serves -Tftp with a perl responder

- Status: Accepted
- Date: 2026-10-28
- Decided by Claude under Stewart's delegation (BL-3938)

## Context

`Record-CurlExchange.ps1 -Tftp` binds a UDP socket on `-Port`, reads curl's request there and answers every
datagram from a second socket on a new port, as RFC 1350 requires. Bash cannot open a UDP socket and socat's
address types cannot move a conversation to a new port, so the port of `Record-CurlExchange.sh` needs another
program for that one mode.

## Decision

`-Tftp` in `Record-CurlExchange.sh` writes a perl script (core modules `IO::Socket::IP` and `IO::Select` only) to
its state directory and runs it as the responder, step for step as `RecorderTftpResponder` in the PowerShell script
(OACK with `tsize`, `blksize` and `timeout`, `-TftpNoOack`, `RRQ`/`WRQ` ERROR replies, `PACKET` injection, `DROP`,
retransmission, the idle time). The shell script checks `-TftpReply` as the PowerShell script does, writes
`datagrams.txt` and `upload.bin` from the responder's files, and writes no `request.bin`. perl is needed only for
`-Tftp`; it ships with Linux and macOS and is not a package. A datagram is logged before it is sent so curl cannot
exit between the send and the log line.

## Consequences

Measured with curl 8.18.0 in WSL against the PowerShell script on Windows, `datagrams.txt`, `upload.bin`,
`stdout.bin`, `stderr.txt` and `exitcode.txt` are byte-identical for a read, a large read, `--tftp-blksize`,
`-TftpNoOack`, a write, `DROP` and the idle time. Differences are curl's own: Windows curl ends its error line with
CRLF, and an injected ACK 0 is answered differently by the two curl builds (exit 28 against 71).
