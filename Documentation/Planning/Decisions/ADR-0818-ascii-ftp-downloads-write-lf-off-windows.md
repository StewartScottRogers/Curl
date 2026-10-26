# ADR-0818: An ASCII FTP download writes LF line endings off Windows

- Status: Accepted
- Date: 2026-10-26
- Decided by Claude under Stewart's delegation (BL-3553, CGR-024).

## Context

curl defines `CURL_PREFER_LF_LINEENDS` everywhere but Windows (`lib/urldata.h`), and there
adds `ftp_cw_lc_write` (`lib/ftp.c`) to every FTP transfer in ASCII mode: a listing (`LIST`
or `NLST`), `-B`, or a `;type=a` URL suffix. It writes each CRLF as LF and each lone CR as
LF, holding a CR that ends one read until the next byte or the end of the transfer. Curl
wrote the bytes unchanged on every platform (CGR-024). Measured with curl 8.18.0 OpenSSL on
Linux (WSL) and 8.22.0 Schannel on Windows against `Record-CurlExchange.ps1 -Ftp`: on Linux
`drw d.txt\r\n-rw e.txt\r\n` is written `drw d.txt\n-rw e.txt\n` and `a\r\nb\rc\r` (`-B`)
`a\nb\nc\n`; on Windows both are written unchanged, and a binary download is unchanged on
both.

## Decision

`FtpDownloadLineEndings` does curl's conversion. `FtpSession.DownloadAsync` makes one for
each download, converting for an ASCII transfer off Windows and passing the bytes through
otherwise; `CopyDataAsync`'s writes go through it, and its `Finish` writes a held CR as LF
once the data connection ends. The conversion sits before the wildcard listing parser too,
as curl's writer sits before `Curl_ftp_parselist`. Byte counts (`size_download`,
`--max-filesize`, `-r`) stay those read from the data connection, as curl counts them before
its content writers; `-v` and `--trace` still show the bytes read.

Tests that pin an ASCII download's output compute it with the test fake
`FtpAsciiOutput.Of`, so they hold on Windows, Linux and macOS.
