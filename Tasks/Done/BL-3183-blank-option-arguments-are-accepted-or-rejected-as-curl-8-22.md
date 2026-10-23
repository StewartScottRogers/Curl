---
id: BL-3183
title: Blank option arguments are accepted or rejected as curl 8.22.0 does (BL-3171 item 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3183 — Blank option arguments are accepted or rejected as curl 8.22.0 does (BL-3171 item 1)

## Goal

Every option that takes an argument accepts or rejects an empty argument (`''`) exactly as curl 8.22.0 does, `--proxy-key ''` included.

## Context

BL-3171 item 1. Parsing is in `Curl.Cli.UnitLibrary/CommandLineOptions.cs` and `CommandLineOptionTable.cs`. Run every argument-taking option once with `''` through curl 8.22.0 and quote the accept/reject list in Notes.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [x] A test in `Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs` runs every option that takes an argument with `''` and matches curl 8.22.0's measured accept/reject list (the list is quoted in Notes); `--proxy-key ''` has its own case.
- [x] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes
- Measured 2026-10-22 with the local Schannel curl 8.22.0: every option `curl --help all` shows with an argument (146), plus `--krb4` and Curl's own value rows, run once as `curl -m 5 --<name> '' http://127.0.0.1:1/` through `Record-CurlExchange.ps1 -NoServer` (each call through `powershell -Command { } -args` so the empty argument survives; `--user ''` and `--proxy-user ''` sat at curl's password prompt, so they accept it).
- Refused as blank (exit 2, `blank argument where content is expected`): abstract-unix-socket capath cert cert-type ciphers cookie-jar curves dump-header engine etag-compare etag-save ftp-account ftp-alternative-to-user ftp-port haproxy-clientip hostpubmd5 hostpubsha256 interface ipfs-gateway key key-type libcurl mail-auth mail-from oauth2-bearer output output-dir pass pinnedpubkey preproxy proto-default proxy-capath proxy-cert proxy-cert-type proxy-ciphers proxy-key-type proxy-pinnedpubkey proxy-service-name proxy-tls13-ciphers proxy1.0 pubkey range request request-target sasl-authzid service-name sigalgs socks4 socks4a socks5 socks5-hostname tls13-ciphers trace trace-ascii unix-socket url
- Accepted (went on to the transfer, exit 7; help exit 0; user and proxy-user prompt): alt-svc aws-sigv4 connect-to cookie data data-ascii data-binary data-raw data-urlencode delegation doh-url egd-file ftp-method ftp-ssl-ccc-mode header help hsts json krb krb4 login-options mail-rcpt noproxy proto proto-redir proxy proxy-header proxy-key proxy-pass proxy-tlsauthtype proxy-tlspassword proxy-tlsuser proxy-user quote random-file referer resolve socks5-gssapi-service stderr telnet-option time-cond tlsauthtype tlspassword tlsuser trace-config upload-file url-query user user-agent variable write-out
- Refused for another reason: not a proper number (connect-timeout continue-at create-file-mode expect100-timeout happy-eyeballs-timeout-ms ip-tos keepalive-cnt keepalive-time limit-rate max-filesize max-redirs max-time parallel-max parallel-max-host rate retry retry-delay retry-max-time speed-limit speed-time tftp-blksize vlan-priority); file does not exist (cacert crlfile knownhosts netrc-file proxy-cacert proxy-crlfile); badly used (form form-string local-port tls-max); unknown (upload-flags); config exit 26; not supported by this build (dns-interface dns-ipv4-addr dns-ipv6-addr dns-servers ech ssl-sessions).
- Curl differed only on `--proxy-key ''` and `--proxy-pass ''` (refused as blank); both now accept it. The c-ares and ECH options keep refusing a blank argument as a build with c-ares and ECH does (ADR-0767). `--log-level`, `--log-file`, `--httpsig-*` are unknown to curl 8.22.0 (Curl's own) and are listed apart, unmeasured.
- Touches kept: the fix landed in CommandLineOptionTable.cs (two appliers, calling CommandLineOption's internal warning helper rather than editing CommandLineOption.cs) and doc comments in CommandLineOptions.cs.
- Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary: 0 failing members. Build -warnaserror clean; Curl.Cli.UnitTests fast tests 4277 passed, 18 skipped. Tests use no path, file or OS text, so they are platform-neutral.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: a0924c4b 6361b4ee.
- 2026-10-22: Doing -> Done. Every argument-taking option accepts or refuses an empty argument as curl 8.22.0 does; --proxy-key '' and --proxy-pass '' are now accepted
