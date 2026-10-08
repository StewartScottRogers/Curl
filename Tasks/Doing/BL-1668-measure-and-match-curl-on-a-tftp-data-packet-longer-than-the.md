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
completed:
---
# BL-1668 — Measure and match curl on a tftp DATA packet longer than the block size

## Goal

A `tftp://` download that receives a DATA packet longer than the agreed block size plus four does what curl 8.21.0 does on each platform, measured, instead of writing the whole oversized payload.

## Context

- Found by BL-1520's adversarial tests. Input: DATA 1 with 600 payload bytes at the default block size 512, then DATA 2 of 1 byte. `TftpDownload` receives into a 65468-byte buffer and writes all 600 bytes (601 in total). Curl receives into a buffer of the block size it asked for plus four (at least 516), so on Linux and macOS `recvfrom` truncates the datagram to 512 payload bytes (513 in total), and on Windows `recvfrom` fails with `WSAEMSGSIZE`, which curl takes as a too-short packet.
- Measure the Schannel build on Windows and the OpenSSL build on Linux with a loopback UDP server (extend `Record-CurlExchange.ps1`) and pin each platform's answer in its own test. The truncation may belong in the datagram channel rather than the handler.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Tftp.UnitTests` pin each platform's measured answer for an oversized DATA packet.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-07: Created by BL-1520.
- 2026-10-07: Backlog -> Doing.
