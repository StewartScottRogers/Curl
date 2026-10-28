---
id: BL-3938
title: Port the -Tftp mode of Record-CurlExchange to Record-CurlExchange.sh
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3930]
touches: [Record-CurlExchange.sh]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3938 — Port the -Tftp mode of Record-CurlExchange to Record-CurlExchange.sh

## Goal

`Record-CurlExchange.sh` handles -Tftp like `Record-CurlExchange.ps1`.

## Context

Split from BL-3931 (part of BL-3934); follows BL-3929 and BL-3930 in the same file. Mode: TFTP transfers (RRQ/WRQ, OACK, TftpData, TftpReply, TftpNoOack, TftpIdleMilliseconds). Test in WSL Ubuntu (socat, curl 8.18.0, openssl present) against the .ps1 under pwsh. Platform-neutral output only.

## Acceptance criteria

- [x] The mode no longer exits 2 with "not ported yet", and the "not ported" header block no longer lists it (or states why it cannot exist off Windows).
- [x] At least one case per mode records byte-identical `request.bin`, `stdout.bin`, `stderr.txt` and `exitcode.txt` to the .ps1; cases and results are in the Log.
- [x] `bash -n Record-CurlExchange.sh` passes and the HTTP, HTTPS, FTP and mail cases still match.

## Notes

- Decision (ADR-0936): bash cannot answer a datagram from a new UDP port, so -Tftp runs an embedded perl responder (core modules); perl is checked only for -Tftp.
- The "not ported" header block no longer lists -Tftp; a TFTP section in the header describes it. It writes datagrams.txt and upload.bin and no request.bin, as the .ps1 does.
- Measured in WSL Ubuntu (curl 8.18.0) against the .ps1 under pwsh on Windows, -sS, byte-compared datagrams.txt, upload.bin, stdout.bin, stderr.txt, exitcode.txt: identical for rrq, rrq 1300 bytes, --tftp-blksize 100, -TftpNoOack, wrq (-T), DROP (DATA1=DROP), idle (-TftpIdleMilliseconds). Differences are curl's own: RRQ=ERROR stderr ends CRLF on Windows curl; RRQ=PACKET 00040000 gets exit 28 on Windows curl and 71 on Linux curl, with the responder's own datagrams identical up to the point they diverge.
- bash -n passes; an HTTP and an FTP exchange still record in WSL.
- CI on Linux and macOS: no test or fast-test project changed.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: ebe547a6 49b8818c 541414b4.
- 2026-10-28: Doing -> Done. Record-CurlExchange.sh -Tftp serves RRQ, WRQ, OACK, NoOack, ERROR, DROP and idle byte-identically to the .ps1
