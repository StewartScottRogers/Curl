---
id: BL-932
title: Answer tftp:// requests from Record-CurlExchange.ps1 with a scripted TFTP responder
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Record-CurlExchange.ps1]
requirement: none
created: 2026-09-29
completed:
---
# BL-932 — Answer tftp:// requests from Record-CurlExchange.ps1 with a scripted TFTP responder

## Goal

`Record-CurlExchange.ps1 -Tftp` serves one TFTP transfer on UDP so real curl's `tftp://` output (`-v`, `--trace`, `--trace-ascii`, exit code) can be measured against a loopback server, as `-Ftp`, `-Smtp`, `-Imap` and `-Pop3` already allow for their protocols.

## Context

- Needed by BL-933 (curl's `-v`/`--trace` lines for TFTP). Today the recorder can only swallow UDP (`-UdpSink`, which never answers), so a TFTP measurement has no server to talk to. Root `CLAUDE.md`: measure real curl with `Record-CurlExchange.ps1` and extend it when it falls short; PowerShell only, no Python.
- Behaviour to add, following the `-Ftp` parameter's style (reply table with overrides):
  - `-Tftp`: bind UDP on `ListenAddress`/`Port`; answer a read request (RRQ) by sending `-TftpData` (default `hello\n`) in DATA blocks of the agreed block size from a fresh ephemeral port, as RFC 1350 requires, waiting for each ACK; answer a write request (WRQ) with ACK 0 (or OACK) and ACK each DATA block, saving the bytes to `upload.bin`.
  - Options (RFC 2347/2348/2349): when the request carries `blksize`, `tsize` or `timeout`, answer with an OACK echoing them (tsize filled in for RRQ) unless `-TftpNoOack` is given.
  - `-TftpReply` overrides, each `STEP=reply`: `RRQ=ERROR 1 File not found` sends an ERROR packet with that code and text instead of data; `ACK<n>=DROP` ignores the n-th ACK once so curl retransmits; `DATA<n>=DROP` does not send block n the first time.
  - Output: `datagrams.txt` with one line per datagram in both directions (`> ` from curl, `< ` from the server, lowercase hex, then a decoded summary such as `RRQ file octet blksize=512 tsize=0`), plus the usual `stdout.bin`, `stderr.txt` and `exitcode.txt`.
- Combining `-Tftp` with `-Ftp`, `-Smtp`, `-Imap`, `-Pop3`, `-Script` or `-Tls` is refused, as the other server modes refuse each other.
- Update the script's comment-based help (`.PARAMETER Tftp`, `TftpData`, `TftpReply`, `TftpNoOack`) in the same style.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Record-CurlExchange.ps1 -Tftp -CurlArgs 'tftp://127.0.0.1:<port>/file'` (with the port it prints or the one given) records curl's exit code 0, standard output `hello` plus a line feed, and a `datagrams.txt` whose first line is curl's RRQ.
- [ ] `-TftpReply 'RRQ=ERROR 1 File not found'` records curl's exit 68 (`CURLE_TFTP_NOTFOUND`).
- [ ] A `-T` upload records the uploaded bytes in `upload.bin`.
- [ ] `Get-Help .\Record-CurlExchange.ps1 -Parameter Tftp` describes the mode.
- [ ] The script still runs every existing mode unchanged (spot-check `-Ftp` and the default HTTP mode once each), and the transcript of all three runs is pasted into Notes.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
