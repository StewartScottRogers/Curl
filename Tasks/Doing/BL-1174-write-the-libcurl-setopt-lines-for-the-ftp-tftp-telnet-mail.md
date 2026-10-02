---
id: BL-1174
title: Write the --libcurl setopt lines for the FTP, TFTP, telnet, mail, SSH, verbose, upload, protocol and remaining options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1106]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1174 — Write the --libcurl setopt lines for the FTP, TFTP, telnet, mail, SSH, verbose, upload, protocol and remaining options

## Goal

The `--libcurl` generator writes curl 8.21.0's lines for every option `LibcurlSourceCodeOptionCoverageTests` lists under `WaitingForBl1170`, so that list is empty and deleted.

## Context

- Split from BL-1106, which wrote the transfer, redirect, speed, time-condition and socket options (`LibcurlSourceCode.TransferOptions.cs`) and classified the options that write nothing. BL-654 did TLS, proxy and authentication (ADR-0326: Schannel lines on every platform).
- Measure with `Record-CurlExchange.ps1 -NoServer -CurlArgs --libcurl,-,-s,...` against `http://127.0.0.1:1/`, and against the option's own scheme (`ftp://`, `tftp://`, `telnet://`, `smtp://`, `imap://`, `sftp://`, `mqtt://`) for the scheme-specific ones: measured on HTTP alone they write nothing, but curl writes their lines only for their scheme.
- Already measured on HTTP by BL-1106 (see its Notes): `-v`, `--trace`, `--trace-ascii` add `CURLOPT_VERBOSE, 1L` and two lines (`CURLOPT_DEBUGFUNCTION`, `CURLOPT_DEBUGDATA`) to the cannot-be-generated list; `-T up.txt` rewrites the URL to `.../up.txt` and adds `CURLOPT_UPLOAD, 1L` and `CURLOPT_INFILESIZE_LARGE`; `--url-query a=b` rewrites the URL (`\?a=b`); `--etag-compare` adds an `If-None-Match:` header from the file; `--limit-rate 1k` changes `CURLOPT_BUFFERSIZE` to `1024L` and adds `MAX_SEND_SPEED_LARGE`/`MAX_RECV_SPEED_LARGE`; `--ip-tos`/`--vlan-priority` add `CURLOPT_SOCKOPTFUNCTION`/`SOCKOPTDATA` and `--mptcp` `CURLOPT_OPENSOCKETFUNCTION` to the cannot-be-generated list; `-Z` drops that list's first lines; `--proto =http,ftp` writes `CURLOPT_PROTOCOLS_STR, "ftp,http"` (sorted) and `--proto-redir` `REDIR_PROTOCOLS_STR`; `--proto-default FTP` writes `CURLOPT_DEFAULT_PROTOCOL, "FTP"` as typed, which `CommandLineOptions.DefaultProtocol` does not keep.
- Refused (exit 2, no file) by the mingw Schannel build: `--log-level`, `--log-file`, `--dns-servers`, `--dns-interface`, `--dns-ipv4-addr`, `--dns-ipv6-addr`, `--ech`, `--ssl-sessions`, `--tlsuser`, `--tlspassword`, `--tlsauthtype`, `--knownhosts`, `--http2`, `--http2-prior-knowledge`, `--http3`, `--http3-only`. Decide (ADR) whether these write nothing, as on Schannel, or the OpenSSL build's lines.
- `-C -` with an existing output file writes `RESUME_FROM_LARGE` with the file's size; BL-1106 writes only an explicit offset.

## Acceptance criteria

- [ ] Measured first; output copied into Notes.
- [ ] `Curl.Cli.UnitTests` reproduce each measured output byte for byte, and `WaitingForBl1170` is gone with the enumeration test passing.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
