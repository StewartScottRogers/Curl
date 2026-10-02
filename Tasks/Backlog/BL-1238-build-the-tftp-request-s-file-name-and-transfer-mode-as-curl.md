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
completed:
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

- [ ] Before the code change, `Notes` holds curl 8.21.0's datagrams, stderr and exit code (recorded with `Record-CurlExchange.ps1 -Tftp`, with `-v`) for `-B tftp://.../f.txt`, `tftp://.../f.txt;mode=netascii`, `tftp://.../f.txt;mode=octet` with `-B`, and the 503- and 504-character names under `--tftp-no-options`.
- [ ] Tests in `Curl.Protocol.Tftp.UnitTests` pin the measured request bytes for `%E9.txt`, `-B` (mode `netascii`) and each `;mode=` suffix (the suffix is not part of the name sent), for a download and for an upload.
- [ ] Tests pin `a%00b` as exit 3 `URL using bad/illegal format or missing URL`, a 504-character name as exit 71 `TFTP filename too long`, and a 503-character name as exit 71 `TFTP buffer too small for options` (and as measured under `--tftp-no-options`), each with no datagram sent.
- [ ] Existing TFTP tests still pass unchanged apart from any that asserted the old UTF-8 or `octet`-only bytes, which now assert the measured ones.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes with no test needing `TestCategory=Integration`; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Protocol.Tftp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
