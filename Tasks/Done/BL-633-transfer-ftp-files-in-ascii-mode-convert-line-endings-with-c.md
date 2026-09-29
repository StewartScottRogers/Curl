---
id: BL-633
title: Transfer FTP files in ASCII mode, convert line endings with --crlf and append with -a
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-632, BL-913]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-633 — Transfer FTP files in ASCII mode, convert line endings with --crlf and append with -a

## Goal

`-B` (and a `;type=a` URL suffix) makes the FTP handler send `TYPE A` and treat the data as curl 8.21.0 does; `--crlf` converts LF to CRLF on FTP uploads (and `file://` uploads, FR-013); `-a` uploads with `APPE` instead of `STOR`; each measured byte for byte.

## Context

- Conformance audit 2026-09-28, row 24 (Major). Options: BL-632.
- Code: `Curl.Protocol.Ftp.UnitLibrary/FtpSession.cs` and the handler (`TYPE I` today; `APPE` is already used for some resume paths, `FtpUploadOffset.cs`); context mapping in `Curl.Console/TransferContextFactory.cs` (`ConvertLineEndings` exists; if ASCII mode or append needs a new `ITransferContext` member, file an Abstractions task for it and depend on it rather than widening this one).
- `Record-CurlExchange.ps1 -Ftp` records the commands and the upload bytes (`upload.bin`).

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Ftp`: `-B` download, `ftp://h/f;type=a`, `-T f --crlf` with LF and CRLF lines, `-T f -a`, and `-T f -a -C -`; commands and `upload.bin` copied into Notes.
- [x] `Curl.Protocol.Ftp.UnitTests` pin the commands and data bytes for each case; a `Curl.Console.UnitTests` test shows `--crlf` reaching a `file://` upload.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- 2026-09-29 (lane 3): `ITransferContext` has `ConvertLineEndings` but no member for `-B` or `-a`, so neither can reach the FTP handler (`TransferContext` is its only implementer). As this task's Context directs, the contract change is filed as BL-913 (touches `Curl.Protocol.Abstractions.UnitLibrary`/`.UnitTests`) and this task depends on it. No code was changed here.
- 2026-09-29 (lane 4): measured curl 8.21.0 (Schannel, Windows) with `Record-CurlExchange.ps1 -Ftp -FtpData 'l1\nl2\r\n'` against `ftp://127.0.0.1:47633/dir/f.txt`, uploading `up.txt` = `61 0a 62 0d 0a 63 0a`. Every session began `USER anonymous | PASS ftp@example.com | PWD | CWD dir`; after that it sent:
  - `-B`: `EPSV | TYPE A | RETR f.txt | QUIT` - no `SIZE`; `-v` says `Getting file with size: -1`.
  - `f.txt;type=a` and `;type=A`: the same, no `-B` needed. `-B` with `;type=i`, `;type=I` or `;type=x`: `TYPE I | SIZE f.txt | RETR f.txt`. `f.txt;type=d` / `;type=D`: `TYPE A | NLST`. `-l` with `f.txt;type=a`: `TYPE A | NLST`. `dir/;type=a`: `TYPE A | LIST`.
  - Not a type code, left in the path: `f.txt;TYPE=A`, `f.txt;type=ab`, `f.txt;type=` (`SIZE f.txt;type=ab` etc.), `f.txt%3Btype=a` (`SIZE f.txt;type=a`, `TYPE I`), `dir;type=a/f.txt` (`CWD dir;type=a`, `TYPE I`).
  - `-B -C 3`: `TYPE A | RETR f.txt | QUIT` - no `REST`, whole file. `-B -r 1-3` and `-B -r -3`: `TYPE A | RETR f.txt | ABOR | QUIT`, three bytes `l1\n` (`Maxdownload = 3`). `-B -r 2-`: whole file, no `ABOR`. `-B --max-filesize 3`: exit 63, `Exceeded the maximum allowed file size (3) with 3 bytes` (the existing part-way check). `-B -I`: `MDTM f.txt | TYPE A | SIZE f.txt | REST 0 | QUIT`.
  - `-T up.txt --crlf`: `TYPE I | STOR f.txt`, `upload.bin` = `61 0d 0a 62 0d 0a 63 0d 0a`, `upload completely sent off: 9 bytes`.
  - `-T up.txt -a`: `TYPE I | APPE f.txt`, `upload.bin` = `61 0a 62 0d 0a 63 0a`. `-T up.txt -a -C -` (server `SIZE` 7): `TYPE I | SIZE f.txt | QUIT`, nothing uploaded. `-T up.txt -B`: `TYPE A | STOR f.txt`, bytes unchanged. `-T up.txt f.txt;type=a`: `TYPE A | STOR f.txt`.
  - The data bytes never change for ASCII mode on the wire. On Windows only, `-B` to stdout writes in C runtime text mode (`6c 31 0d 0a 6c 32 0d 0d 0a`; `-o` file unchanged; `;type=a` alone unchanged). That is the tool's stdout, not the FTP handler: filed as BL-956 rather than widening this task.
- Implemented: `FtpTypeCode` reads the suffix (first `;type=` in the still-encoded path, counted only when exactly one character follows); `FtpSession` sends `TYPE A` for ASCII downloads, uploads and `-I`, skips `SIZE`/`REST` for an ASCII download keeping only a range's byte limit, sends `APPE` under `-a`, and converts `--crlf` uploads with its own `CrlfUploadConverter` (a copy of `Curl.Protocol.File`'s: protocol libraries never reference each other, and moving it to Abstractions would widen `touches` for 25 lines). `TransferContextFactory` now maps `-B`, `-a` and `--crlf` - `--crlf` never reached `file://` before this task.
- `--ai-help` needs no change: `--use-ascii`, `--append` and `--crlf` were already there with curl's text. No ADR: every behaviour here was measured, not chosen.
- Not measured and not handled: a `;type=` in the host part (`ftp://h;type=a/f`), which curl's source also looks for.
- One `Curl.Conformance.UnitTests` test failed once under the coverage collector and passed on the rerun and in the plain fast run; unrelated to FTP.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Waits on BL-913: ITransferContext needs UseAscii and Append before -B and -a can reach the FTP handler
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -B and ;type=a send TYPE A, ;type=d lists with NLST, -a uploads with APPE, --crlf converts FTP and file:// uploads, each pinned to curl 8.21.0
