---
id: BL-638
title: Honour --max-filesize for FTP downloads through SIZE
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-638 — Honour --max-filesize for FTP downloads through SIZE

## Goal

An FTP download larger than `--max-filesize` fails with exit 63 (`CURLE_FILESIZE_EXCEEDED`) and curl 8.21.0's message: before `RETR` when `SIZE` tells the size, and part-way when it does not, as curl does.

## Context

- Conformance audit 2026-09-28, row 26 (Major): FTP ignores `--max-filesize`.
- `ITransferContext.MaxFileSize` exists; HTTP (FR-084, ADR-0044) and `file://` (FR-015) are the models. Only the FTP handler changes.
- `Record-CurlExchange.ps1 -Ftp` answers `SIZE` with `FtpData`'s length.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp`: `--max-filesize` below and above the size, and with `SIZE` answered `550`; commands, stdout bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin commands, bytes written and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-29 against curl 8.21.0 (Schannel, Windows) with
`Record-CurlExchange.ps1 -Port 40638 -Ftp -FtpData 'Hello, world!\n'` (14 bytes),
URL `ftp://127.0.0.1:40638/f.txt`, `-s -S`. Every run sent
`USER anonymous`, `PASS ftp@example.com`, `PWD`, `EPSV`, `TYPE I`, `SIZE f.txt` first.

| Case | Commands after `SIZE` | stdout | stderr | exit |
| --- | --- | --- | --- | --- |
| `--max-filesize 5` (SIZE 213 14) | `QUIT` | empty | `curl: (63) Maximum file size exceeded` | 63 |
| `--max-filesize 14` | `RETR`, `QUIT` | `Hello, world!\n` | empty | 0 |
| `--max-filesize 100` | `RETR`, `QUIT` | `Hello, world!\n` | empty | 0 |
| `--max-filesize 10 -C 10` | `QUIT` (no `REST`) | empty | `curl: (63) Maximum file size exceeded` | 63 |
| `--max-filesize 10 -r 0-3` | `ABOR`, `QUIT` | empty | `curl: (63) Maximum file size exceeded` | 63 |
| `--max-filesize 5`, `SIZE=550 No such file` | `QUIT` | empty | `curl: (78) The file does not exist` | 78 |
| `--max-filesize 5`, `SIZE=500 Unknown command` | `RETR`, no `QUIT` | `Hello` | `curl: (63) Exceeded the maximum allowed file size (5) with 5 bytes` | 63 |
| `--max-filesize 14`, SIZE 500 | `RETR`, `QUIT` | `Hello, world!\n` | empty | 0 |
| `--max-filesize 5 -C 4`, SIZE 500 | `REST 4`, `RETR`, no `QUIT` | `o, wo` | `curl: (63) Exceeded the maximum allowed file size (5) with 5 bytes` | 63 |
| `ftp://…/ --max-filesize 5` (listing) | `TYPE A`, `LIST`, no `QUIT` | `Hello` | `curl: (63) Exceeded the maximum allowed file size (5) with 5 bytes` | 63 |
| `-T file --max-filesize 5` (upload) | `STOR`, `QUIT` | empty | empty | 0 |

So a `550` to `SIZE` is still exit 78 (the limit never comes into it); the size-unknown case
the goal means is a `SIZE` curl cannot read, such as `500`. Uploads ignore the limit, as
the code already did.

Implementation: `FtpSession.RefuseOversizedFileAsync` compares the whole `SIZE` count with
the limit before `REST`/`RETR` (through `EndAndFailAsync`, so a range sends `ABOR`), and
`CopyDataAsync` caps each write at what still fits under the limit, counted from this
transfer's first byte (the `-C 4` row), failing with no `QUIT` once more arrives than fits.
`--max-filesize 0` is no limit, as in curl. No design choice beyond matching curl, so no
ADR. Tests: `FtpProtocolHandlerMaxFileSizeTests` (12).

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. FTP --max-filesize fails with exit 63 before RETR when SIZE is over the limit, and part-way when the size is unknown, as curl 8.21.0 does
