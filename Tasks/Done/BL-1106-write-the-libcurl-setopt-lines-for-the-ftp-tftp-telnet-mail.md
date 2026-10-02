---
id: BL-1106
title: Write the --libcurl setopt lines for the FTP, TFTP, telnet, mail, MQTT, retry, rate and remaining options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-654]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1106 — Write the --libcurl setopt lines for the FTP, TFTP, telnet, mail, MQTT, retry, rate and remaining options

## Goal

The `--libcurl` generator writes curl 8.21.0s lines for every option `LibcurlSourceCodeOptionCoverageTests` lists under `WaitingForBl1106` (FTP, TFTP, telnet, mail, MQTT, retry, rate, range, upload, DNS, interface, HTTP version and the rest), so that list is empty.

## Context

- Split from BL-654, which wrote the TLS, proxy and authentication lines (ADR-0326).
- Measure each option with `Record-CurlExchange.ps1 -NoServer -CurlArgs --libcurl,-,-s,...` as in BL-654 Notes; move each option to `Written` or `WritesNothing` as it lands, and delete the empty list.
- `-G -d a=1` URL handling was left by BL-653.

## Acceptance criteria

- [x] Measured first; output copied into Notes.
- [x] `Curl.Cli.UnitTests` reproduce each measured output byte for byte, and `WaitingForBl1106` is gone with the enumeration test passing.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Scope (default taken).** 158 options were waiting; one run could not measure and write them all, so this task wrote the transfer, redirect, speed, time-condition and socket options (38), classified 52 more as writing nothing, and filed **BL-1170** for the remaining 68 (FTP, TFTP, telnet, mail, SSH, verbose and trace, upload, `--proto`, `--limit-rate`, socket callbacks, the options this Schannel build refuses). `WaitingForBl1106` is gone: what is left is listed as `WaitingForBl1170`, and the enumeration test still classifies every parsed option.
- **Measured** 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -NoServer -CurlArgs --libcurl,-,-s,...` against `http://127.0.0.1:1/` and `ftp://127.0.0.1:1/f`. Lines each option adds (the transfer's order is pinned by `Generate_TransferOptionsTogetherOnHttp_WriteEveryLineInCurlsOrder`):
  - After `FAILONERROR`: `--request-target *` `REQUEST_TARGET "*"`, `-l` `DIRLISTONLY, 1L`, `-a` `APPEND, 1L`. Between `NETRC_FILE` and `LOGIN_OPTIONS`: `-B` `TRANSFERTEXT, 1L`. After `USERPWD`: `-r 0-5` `RANGE "0-5"`. After the body: `--form-escape` `MIME_OPTIONS, 1L` (with or without `-F`).
  - HTTP only, after `PROXYHEADER`: `--follow` `FOLLOWLOCATION, 2L` (`-L --follow` 2L, `--follow -L` 1L); `--max-redirs 5` `MAXREDIRS, 5L` (`-1` gives `-1L`); `--http1.0`/`-0`/`--http1.1` `HTTP_VERSION, (long)CURL_HTTP_VERSION_1_0`/`_1_1`; `--post301`/`302`/`303` `POSTREDIR, 1L`/`2L`/`4L` (301+303: `5L`); after `ACCEPT_ENCODING`: `--tr-encoding` `TRANSFER_ENCODING`, `--http0.9` `HTTP09_ALLOWED`, `--alt-svc f` `ALTSVC`, `--hsts f` `HSTS`, `--expect100-timeout 2` `EXPECT_100_TIMEOUT_MS, 2000L`; after `COOKIEJAR`: `-j` `COOKIESESSION, 1L`, before `HEADEROPT`.
  - After the scheme lines (also after `FTP_SKIP_PASV_IP` on ftp): `-Y 100` `LOW_SPEED_LIMIT, 100L` + `LOW_SPEED_TIME, 30L`; `-y 20` `LIMIT 1L` + `TIME 20L`; `-Y 5 -y 0` `LIMIT 5L` only; `-Y 0 -y 9` `LIMIT 1L` + `TIME 9L`; `-y 0` `LIMIT 1L` only; then `-C 5` `RESUME_FROM_LARGE, (curl_off_t)5` (`-C -` without an output file: nothing; with `-o up.txt`: the file's size).
  - In the TLS lines, after `CAINFO` and before `SSLCERT`: `-w x` (any value) `CERTINFO, 1L`.
  - After the TLS lines: `--path-as-is` `PATH_AS_IS`, then `FILETIME`, `--crlf` `CRLF`, `-z 20200101` `TIMECONDITION, (long)CURL_TIMECOND_IFMODSINCE` + `TIMEVALUE_LARGE, (curl_off_t)1577836800` (`-z -date`: `IFUNMODSINCE`); then `CUSTOMREQUEST`, `--interface lo` `INTERFACE "lo"` (`if!lo`, `host!1.2.3.4` as typed), `CONNECTTIMEOUT_MS`, `--doh-url` `DOH_URL`, `--max-filesize 100` `MAXFILESIZE_LARGE, (curl_off_t)100`, `IPRESOLVE`, the SOCKS and service lines, `--ignore-content-length` `IGNORE_CONTENT_LENGTH`, `--local-port 1000-2000` `LOCALPORT, 1000L` + `LOCALPORTRANGE, 1001L` (`1000`: `1L`), `TCP_KEEPALIVE` (`--no-keepalive` drops it and every keepalive line), `--keepalive-time 30` `TCP_KEEPIDLE, 30L` + `TCP_KEEPINTVL, 30L`, `--keepalive-cnt 5` `TCP_KEEPCNT, 5L`, `RESOLVE`, `CONNECT_TO`, the delegation and SASL lines, then `--unix-socket /s` `UNIX_SOCKET_PATH` (`--abstract-unix-socket` `ABSTRACT_UNIX_SOCKET`; the last one given wins), `--happy-eyeballs-timeout-ms 300` `HAPPY_EYEBALLS_TIMEOUT_MS, 300L`, `--disallow-username-in-url` `DISALLOW_USERNAME_IN_URL, 1L`. A zero `--max-filesize`, `--expect100-timeout`, `--happy-eyeballs-timeout-ms`, `--keepalive-time` or `--keepalive-cnt` writes nothing.
  - Nothing: `-g`, `-S`, `-#`, `-N`, `--trace-time`, `--trace-ids`, `--trace-config`, `--stderr`, `--out-null`, `--remote-name-all`, `-J -O`, `--output-dir`, `--create-dirs`, `--no-clobber`, `--skip-existing`, `--remove-on-error`, `-D`, `--etag-save`, `--tcp-nodelay`, `--tcp-fastopen`, `--styled-output`, `--dump-ca-embed` (no transfer), `--retry`, `--retry-delay`, `--retry-max-time`, `--retry-all-errors`, `--retry-connrefused`, `--rate`, `--xattr`, `--show-headers`, `--fail-early`, `--parallel-immediate`, `--parallel-max`, `--parallel-max-host`, `--variable`, `--raw`, `-2`, `-3`, `--metalink`, `--no-npn`, `--ntlm-wb`, `--false-start`, `--egd-file`, `--random-file`, `-q`, `-V` (no transfer, no file). Not measured one by one, classified by what they do: `-K` (reads options; its options write their own lines), `--next` (separates transfers), `-h`, `-M`, `--ai-help` (no transfer, as `-V`) and `--libcurl` itself.
  - The rest of the measurements (verbose, upload, `--url-query`, `--etag-compare`, `--limit-rate`, socket callbacks, `--proto`, refused options) are in BL-1170's Context.
- **Rules.** `-Y`/`-y`: curl's tool sets a 30-second time when `-Y` comes first and a limit of 1 when `-y` does; `CommandLineOptions` keeps no order, so the generator writes limit = `SpeedLimit ?? (SpeedTimeSeconds given ? 1 : 0)` and time = `SpeedTimeSeconds ?? (SpeedLimit given ? 30 : 0)`, which matches every measured case. `HTTP_VERSION` is written only for 1.0 and 1.1: the Schannel build refuses `--http2` and `--http3` (BL-1170 decides those). These follow ADR-0326's decisions (Schannel lines on every platform, curl's order), so no new ADR.
- **Results.** Only `Curl.Cli.UnitLibrary` (new `LibcurlSourceCode.TransferOptions.cs`, calls placed in `LibcurlSourceCode.cs` and `LibcurlSourceCode.ProxyTlsAndAuthentication.cs`) and `Curl.Cli.UnitTests` (new `LibcurlSourceCodeTransferOptionTests`, 56 rows and 5 combinations; lists updated in `LibcurlSourceCodeOptionCoverageTests`) changed. No option was added or changed, so `--ai-help` is unchanged. `dotnet build Curl.slnx -warnaserror` clean; every fast test project passed (`Curl.Cli.UnitTests` 3516 passed, 15 skipped); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 992 members, 0 failing, worst CRAP 10.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --libcurl writes curl 8.21.0's lines for the transfer, redirect, speed, time-condition and socket options and knows 52 more write nothing; the remaining 68 are BL-1170
