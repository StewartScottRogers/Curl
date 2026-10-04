---
id: BL-1395
title: Write curl's 'Uploaded unaligned file size (N out of M bytes)' -v line when an FTP upload of a known size fails before sending it all
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1395 — Write curl's 'Uploaded unaligned file size (N out of M bytes)' -v line when an FTP upload of a known size fails before sending it all

## Goal

When an FTP upload of a known size (`-T <file>`) ends with exit 9, 25 or another status curl's `ftp_done` treats as leaving the connection usable, before all its bytes went out, `-v` shows curl 8.21.0's `* Uploaded unaligned file size (<sent> out of <size> bytes)` after `* Remembering we are in directory ...` (when written) and before `* Connection #0 to host ... left intact`; the exit code and the `curl: (N)` message stay those of the first failure.

## Context

- Today no such text exists in `Curl.Protocol.Ftp.UnitLibrary` (`git grep "Uploaded unaligned"` finds nothing); `FtpTransferMessages.UploadRefused` (`Failed FTP upload: <code>`) and the CWD refusal end the upload with no further line.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/ftp.c`: `ftp_done` (line 3813) first maps the status through `ftp_done_status`, which turns `CURLE_BAD_DOWNLOAD_RESUME`, `CURLE_FTP_WEIRD_PASV_REPLY`, `CURLE_FTP_PORT_FAILED`, `CURLE_FTP_ACCEPT_FAILED`, `CURLE_FTP_ACCEPT_TIMEOUT`, `CURLE_FTP_COULDNT_SET_TYPE`, `CURLE_FTP_COULDNT_RETR_FILE`, `CURLE_PARTIAL_FILE`, `CURLE_UPLOAD_FAILED`, `CURLE_REMOTE_ACCESS_DENIED`, `CURLE_FILESIZE_EXCEEDED`, `CURLE_REMOTE_FILE_NOT_FOUND` and `CURLE_WRITE_ERROR` (when not premature) into `CURLE_OK`; then `ftp_done_check_partial` (lines 3763-3811) for an upload whose body was to be sent (`PPTRANSFER_BODY`) with a known size (`infilesize != -1`) writes `failf(data, "Uploaded unaligned file size (%" FMT_OFF_T " out of %" FMT_OFF_T " bytes)", writebytecount, infilesize)` when, without `--crlf`/`-B`, the bytes sent differ from the size, or, with either, are fewer. That `failf` is a `-v` line only: the error buffer keeps the first message and the transfer's exit code is the original status.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -Ftp`, uploading the 92-byte `C:/Windows/win.ini` (any 92-byte file serves in tests):
  - `-sv -T win.ini ftp://127.0.0.1:PORT/d/f` with `CWD=550 No such dir`: `> CWD d`, `< 550 No such dir`, `* Server denied you to change to the given directory`, `* Uploaded unaligned file size (0 out of 92 bytes)`, `* Connection #0 to host 127.0.0.1:PORT left intact`; exit 9. The same with `--ftp-create-dirs` and `MKD=550 Denied` (after `> MKD d`, `< 550 Denied`, `> CWD d`, `< 550 No such dir`).
  - `-sv -T win.ini ftp://127.0.0.1:PORT/f` with `STOR=553 Not allowed`: `< 553 Not allowed`, `* Failed FTP upload: 553`, `* Remembering we are in directory ""`, `* Uploaded unaligned file size (0 out of 92 bytes)`, `* Connection #0 ... left intact`; exit 25. With `-sS` stderr is only `curl: (25) Failed FTP upload: 553`. With `--crlf` the line is written too.
  - The same `STOR=553` with `-T -` (standard input, size unknown): no such line.

## Acceptance criteria

- [ ] Tests in `Curl.Protocol.Ftp.UnitTests` over the fake FTP server upload a 92-byte source and pin, in order, the measured `-v` lines for the CWD-refused case (exit 9) and the STOR-553 case (exit 25), each with `Uploaded unaligned file size (0 out of 92 bytes)` and the message of the first failure as the result's message.
- [ ] Tests pin no such line for an upload of unknown size (standard input), for a successful upload, and for a failure `ftp_done_status` does not map to OK (a login refused with exit 67).
- [ ] A test pins the `--crlf` (line-ending conversion) STOR-553 case with the line.
- [ ] `dotnet build Curl.Protocol.Ftp.UnitTests -warnaserror` is clean; `dotnet test Curl.Protocol.Ftp.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary` reports no failing member in the code this task changed.

## Notes

- Tests must not read `C:/Windows/win.ini`; use an in-memory 92-byte source so they pass on Linux and macOS.

## Log

- 2026-10-03: Created.
