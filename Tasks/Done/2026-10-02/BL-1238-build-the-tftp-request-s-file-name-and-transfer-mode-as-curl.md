---
id: BL-1238
title: Build the TFTP request's file name and transfer mode as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1237]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests]
requirement: FR-032
created: 2026-10-02
completed: 2026-10-02
---
# BL-1238 — Build the TFTP request's file name and transfer mode as curl does

## Goal

The read and write requests `TftpProtocolHandler` sends carry the file name as the raw bytes curl 8.21.0 percent-decodes from the URL, the mode `netascii` under `-B`/`--use-ascii` or a `;mode=netascii` URL suffix, and are refused before sending exactly where curl refuses them: a decoded NUL (exit 3), a name too long for the 512-byte packet (exit 71 `TFTP filename too long`) and options that no longer fit (exit 71 `TFTP buffer too small for options`).

## Context

- Today `Curl.Protocol.Tftp.UnitLibrary/TftpProtocolHandler.cs` `TransferFileAsync` decodes the name with `Uri.UnescapeDataString`, which leaves an escape that is not UTF-8 as written, and `TftpPackets.BuildRequest` (`TftpPackets.cs`) writes every field with `Encoding.UTF8` and always the mode `octet`. `ITransferContext.UseAscii` (`-B`) is never read, and nothing checks the length or a NUL.
- curl 8.21.0, `lib/tftp.c` at `curl-8_21_0` (https://github.com/curl/curl/blob/curl-8_21_0/lib/tftp.c):
  - `tftp_setup_connection` (lines 1324-1332) cuts a trailing `;mode=netascii` (sets ASCII mode) or `;mode=octet` (clears it) off the URL path before anything else;
  - `tftp_send_first` (lines 660-745): mode is `netascii` when `-B` or the suffix set ASCII mode, else `octet`; an empty name is `Missing filename` (already built, FR-036); the name is decoded with `Curl_urldecode(..., REJECT_ZERO)`, so a `%00` is exit 3 (`CURLE_URL_MALFORMAT`); when `strlen(name) + strlen(mode) + 4 > 512` it is exit 71 `TFTP filename too long`; each option is then added by `tftp_option_add` (lines 332-345), which fails with exit 71 `TFTP buffer too small for options` once an option no longer fits the 512-byte packet. `--tftp-no-options` adds none.
- Measured 2026-10-02 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1 -Tftp -CurlArgs '-sS','tftp://127.0.0.1:<port>/<path>'`:
  - `%E9.txt`: the RRQ starts `00 01 e9 2e 74 78 74 00 6f 63 74 65 74 00` (the raw byte 0xE9), exit 0;
  - `a%00b`: no datagram, `curl: (3) URL using bad/illegal format or missing URL`, exit 3;
  - a 503-character name: no datagram, `curl: (71) TFTP buffer too small for options`, exit 71;
  - a 504-character name: no datagram, `curl: (71) TFTP filename too long`, exit 71.
- The URL's path reaches the handler as `context.Url.AbsolutePath`; percent-decode it to bytes yourself (the BCL's `Uri.UnescapeDataString` decodes to UTF-16 and cannot keep a lone 0xE9).

## Acceptance criteria

- [x] Before the code change, `Notes` holds curl 8.21.0's datagrams, stderr and exit code (recorded with `Record-CurlExchange.ps1 -Tftp`, with `-v`) for `-B tftp://.../f.txt`, `tftp://.../f.txt;mode=netascii`, `tftp://.../f.txt;mode=octet` with `-B`, and the 503- and 504-character names under `--tftp-no-options`.
- [x] Tests in `Curl.Protocol.Tftp.UnitTests` pin the measured request bytes for `%E9.txt`, `-B` (mode `netascii`) and each `;mode=` suffix (the suffix is not part of the name sent), for a download and for an upload.
- [x] Tests pin `a%00b` as exit 3 `URL using bad/illegal format or missing URL`, a 504-character name as exit 71 `TFTP filename too long`, and a 503-character name as exit 71 `TFTP buffer too small for options` (and as measured under `--tftp-no-options`), each with no datagram sent.
- [x] Existing TFTP tests still pass unchanged apart from any that asserted the old UTF-8 or `octet`-only bytes, which now assert the measured ones.
- [x] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-10-02 with curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Tftp -Port 47238`, `-sSv`, before the code change:

- `-B tftp://.../f.txt`: RRQ `0001 662e747874 00 6e65746173636969 00 7473697a65 00 30 00 626c6b73697a65 00 353132 00 74696d656f7574 00 36 00` (`f.txt netascii tsize=0 blksize=512 timeout=6`), exit 0. stderr: `Trying`, `Established`, `set timeouts for state 0; Total 300000, retry 6 maxtry 50`, OACK lines, `Connected for receive`, `set timeouts for state 1; ...`, `shutting down connection #0`.
- `tftp://.../f.txt;mode=netascii`: the same RRQ (`f.txt netascii ...`; the suffix is not sent), exit 0.
- `-B tftp://.../f.txt;mode=octet`: RRQ `f.txt octet tsize=0 blksize=512 timeout=6`, exit 0 (the suffix overrides `-B`).
- `-T global.json tftp://.../f.txt;mode=netascii`: WRQ `0002 f.txt\0netascii\0tsize\087\0blksize\0512\0timeout\06\0`, exit 0.
- `--tftp-no-options`, 503 `a`s: RRQ sent, 512 bytes (`0001 a*503 00 octet 00`), exit 0.
- `--tftp-no-options`, 504 `a`s: no datagram; stderr `Trying`, `Established`, `set timeouts for state 0; ...`, `* TFTP filename too long`, `* shutting down connection #0`, `curl: (71) TFTP filename too long`; exit 71.
- 503 `a`s with options: no datagram; `* TFTP buffer too small for options`, `* shutting down connection #0`, `curl: (71) TFTP buffer too small for options`; exit 71.
- `%E9.txt;mode=octet`: RRQ `0001 e9 2e747874 00 octet ...`, exit 0.
- `a%00b`: no datagram; `Trying`, `Established`, `set timeouts for state 0; ...`, `* shutting down connection #0`, `curl: (3) URL using bad/illegal format or missing URL` (no `-v` line for the failure); exit 3.

Choices (sensible defaults, per curl 8.21.0's `lib/tftp.c`):
- The checks run where curl runs them, in `tftp_send_first`: after the channel opens and the state-0 timeouts line, before any datagram, so `-v` matches. Each exit 71 also reports curl's `failf` text as a `-v` line (`TftpTransferEvents.Refused`); exit 3 reports none, as measured.
- curl's request buffer limit is `state->blksize`, always 512 when the request is built (the OACK has not arrived), whatever `--tftp-blksize` asks for; so the limit is 512 bytes total. `tftp_option_add` failing for any option is the same as the whole request exceeding 512 bytes, which is what `TftpRequestFile.TryBuildRequest` checks.
- Percent-decoding follows `Curl_urldecode`: `%` and two hex digits (either case) is that byte, anything else is kept as written. The `Missing filename` check stays before the channel opens (FR-036, unchanged) and now runs on the name after the mode suffix is cut, as curl's `tftp_setup_connection` cuts it first.
- The diagnostic log keeps naming the file as `Uri.UnescapeDataString` decodes it (unchanged log text).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. TFTP requests carry curl's percent-decoded name bytes and netascii mode, and refuse %00 (exit 3) and over-long names/options (exit 71) as curl does
