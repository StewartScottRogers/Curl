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
completed: 2026-10-02
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

- [x] Measured first; output copied into Notes.
- [x] `Curl.Cli.UnitTests` reproduce each measured output byte for byte, and `WaitingForBl1170` is gone with the enumeration test passing.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Scope (default taken).** Wrote 41 options and classified 28 as writing nothing; `-T` and `--etag-compare` need the console's per-transfer file knowledge (which upload file, its size, the ETag file's contents), so they moved to **BL-1177** (`WaitingForBl1177`), with curl's SSH known-hosts failure and `-C -` with an existing file. `WaitingForBl1170` is gone. Added `Curl.Cli.UnitLibrary` internals `FtpFileMethodGiven` and `DefaultProtocolAsTyped` (both in `touches`).
- **Measured** 2026-10-02, curl 8.21.0 mingw Schannel, `Record-CurlExchange.ps1 -NoServer -CurlArgs --libcurl,-,-s,...` on http, ftp, tftp, telnet, smtp, imap, sftp, scp:
  - `-v`/`--trace`/`--trace-ascii`: `CURLOPT_VERBOSE, 1L` before `BUFFERSIZE`; `CURLOPT_DEBUGFUNCTION`/`DEBUGDATA` first in the cannot-be-generated list. `--mptcp` adds `OPENSOCKETFUNCTION` after the standard list, non-zero `--ip-tos`/`--vlan-priority` `SOCKOPTFUNCTION`/`SOCKOPTDATA` after that (zero: nothing). `-Z`: every setopt line, but no list and no `curl_easy_perform` line at all.
  - `--limit-rate N`: `BUFFERSIZE` becomes N when below 102400; `MAX_SEND_SPEED_LARGE`/`MAX_RECV_SPEED_LARGE (curl_off_t)N` after `LOW_SPEED_*`, before `RESUME_FROM_LARGE`; 0 writes nothing.
  - `--url-query a=b`: URL `"...\?a=b"`, after an own query `&a=b&c=d`; `-G -d a=1` puts `\?a=1` in the URL and drops `--url-query`.
  - FTP only, in order: `FTPPORT "-"` (`--ftp-pasv` clears it), `FTP_SSL_CCC (long)CURLFTPSSL_CCC_PASSIVE`/`_ACTIVE`, `FTP_ACCOUNT`, `FTP_SKIP_PASV_IP, 1L` (gone with `--no-ftp-skip-pasv-ip`), `FTP_FILEMETHOD` 1L/2L/3L for multicwd/nocwd/singlecwd (any `--ftp-method` given, even a bogus one as 1L), `FTP_ALTERNATIVE_TO_USER`, `FTP_USE_PRET`. `--disable-epsv`, `--epsv`, `--disable-eprt`, `--eprt`: nothing.
  - Every scheme: `USE_SSL` after `PROXY_SSL_CIPHER_LIST`, before `SSL_ENABLE_ALPN` (`--ssl-reqd` ALL outranks `--ftp-ssl-control` CONTROL outranks `--ssl` TRY); after `CRLF`: `QUOTE`, `POSTQUOTE` (`-` prefix, stripped), `PREQUOTE` (`+`); after `INTERFACE`: `TELNETOPTIONS`; after `DOH_URL`: `FTP_CREATE_MISSING_DIRS, 2L`; after the keepalive lines: `TFTP_BLKSIZE` (any value, 512 too), `MAIL_FROM`, `MAIL_RCPT`, `MAIL_RCPT_ALLOWFAILS`, `NEW_FILE_PERMS` (0644 → 420L, 0 → nothing), `PROTOCOLS_STR`, `REDIR_PROTOCOLS_STR`, then `RESOLVE`, `CONNECT_TO`, `GSSAPI_DELEGATION`, `MAIL_AUTH`, `SASL_AUTHZID`, `SASL_IR`, `UNIX_SOCKET_PATH`, `DEFAULT_PROTOCOL` (as typed: `FtP`), `TFTP_NO_OPTIONS`, `HAPPY_EYEBALLS_TIMEOUT_MS`, `DISALLOW_USERNAME_IN_URL`, `UPLOAD_FLAGS` (written unless `\Seen` alone: deleted 18L, answered 17L, draft 20L, `-seen` 0L).
  - `--proto =HTTP` → `"http"`; `all` → `"dict,file,ftp,ftps,gopher,gophers,http,https,imap,imaps,ldap,ldaps,mqtt,mqtts,pop3,pop3s,rtsp,scp,sftp,smtp,smtps,telnet,tftp,ws,wss"` (no smb); `-all` and `=smb` refused.
  - scp/sftp only, after `KEYPASSWD`: `SSH_PRIVATE_KEYFILE` (`--key`, which still writes `SSLKEY` too), `SSH_PUBLIC_KEYFILE`, `SSH_HOST_PUBLIC_KEY_MD5`, `SSH_HOST_PUBLIC_KEY_SHA256`, `SSH_COMPRESSION`. Without a host key hash and without `-k`, curl fails setting the known-hosts file (exit 2, output cut off): BL-1177.
  - Nothing: `--doh-insecure`, `--doh-cert-status`, `--krb`, `--krb4`, `--parallel` of its own, `--ipfs-gateway` (the console passes the gateway URL). Refused by this build: see ADR-0362.
- **Decision.** ADR-0362 (decided by Claude under Stewart's delegation): options the Schannel build refuses write nothing, and `--proto all` leaves out smb/smbs, following ADR-0326.
- **Results.** New `LibcurlSourceCode.ProtocolOptions.cs` and `LibcurlSourceCodeProtocolOptionTests` (byte-for-byte rows and two whole-order combinations); coverage lists updated. No option added or changed, so `--ai-help` is unchanged. `dotnet build Curl.slnx -warnaserror` clean; fast tests green across the solution; `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 0 failing members.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --libcurl writes curl 8.21.0's lines for the FTP, SSH, TFTP, telnet, mail, verbose, rate-limit, query and protocol options; -T and --etag-compare moved to BL-1177
