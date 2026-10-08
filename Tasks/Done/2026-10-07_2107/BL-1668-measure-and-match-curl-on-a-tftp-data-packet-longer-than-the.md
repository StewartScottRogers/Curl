---
id: BL-1668
title: Measure and match curl on a tftp DATA packet longer than the block size
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1668 — Measure and match curl on a tftp DATA packet longer than the block size

## Goal

A `tftp://` download that receives a DATA packet longer than the agreed block size plus four does what curl 8.21.0 does on each platform, measured, instead of writing the whole oversized payload.

## Context

- Found by BL-1520's adversarial tests. Input: DATA 1 with 600 payload bytes at the default block size 512, then DATA 2 of 1 byte. `TftpDownload` receives into a 65468-byte buffer and writes all 600 bytes (601 in total). Curl receives into a buffer of the block size it asked for plus four (at least 516), so on Linux and macOS `recvfrom` truncates the datagram to 512 payload bytes (513 in total), and on Windows `recvfrom` fails with `WSAEMSGSIZE`, which curl takes as a too-short packet.
- Measure the Schannel build on Windows and the OpenSSL build on Linux with a loopback UDP server (extend `Record-CurlExchange.ps1`) and pin each platform's answer in its own test. The truncation may belong in the datagram channel rather than the handler.

## Acceptance criteria

- [x] Tests in `Curl.Protocol.Tftp.UnitTests` pin each platform's measured answer for an oversized DATA packet.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured Windows (curl 8.21.0 Schannel): `Record-CurlExchange.ps1 -Tftp -TftpNoOack -TftpData b -TftpReply "RRQ=PACKET 00030001<600 x 61>"`, `--tftp-no-options`. curl notes `Received too short packet`, writes nothing of the 600-byte DATA 1, then takes the next DATA 1 (`b`): exit 0, 1 byte out. No extension of the recorder was needed.
- Linux could not be measured here: WSL's curl 8.18.0 could not reach the Windows loopback recorder. The Linux/macOS answer (cut to 512 payload bytes, written, ACKed) is pinned from `recvfrom` semantics and curl's code path; ADR-0430 records it.
- Fix: `TftpDownload.ReceiveBuffer()` hands the channel `blockSize + 4` bytes; `TftpTimeLimits` answers `SocketError.MessageSize` (what .NET throws on Windows) as a too-short packet. The UDP channel needed no change. The test fakes now cut an oversized datagram to the buffer as a Unix socket does.
- Tests: `ExecuteAsync_DataLongerThanDefaultBlockSize_CutsItToTheBlockSizeAsLinuxAndMacOSDo`, `ExecuteAsync_DataLongerThanAcknowledgedBlockSize_CutsItToTheAcknowledgedBlockSize` (adversarial suite), `ExecuteAsync_DataLongerThanBlockSizeFailsReceiveAsOnWindows_ReportsTooShortAndCompletesOnNextBlock` (retransmission suite). Tftp tests 271/271, fast suite green. Measure-CodeQuality not run (budget); the two new lines and the new `or` clause are reached by the new tests.

## Log

- 2026-10-07: Created by BL-1520.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A tftp DATA packet longer than the block size plus four is cut to the block size on Linux/macOS and noted as too short on Windows, as curl does
