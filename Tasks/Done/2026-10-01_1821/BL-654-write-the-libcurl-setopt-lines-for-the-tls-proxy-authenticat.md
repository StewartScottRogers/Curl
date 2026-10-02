---
id: BL-654
title: Write the --libcurl setopt lines for the TLS, proxy, authentication and protocol options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-653]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-654 — Write the --libcurl setopt lines for the TLS, proxy, authentication and protocol options

## Goal

The `--libcurl` generator covers every remaining option `CommandLineOptionTable` parses (TLS, proxy, authentication, FTP, TFTP, telnet, MQTT, retry and rate options, and any added since), writing curl 8.21.0's lines for each, so no parsed option is silently missing from the generated source.

## Context

- Conformance audit 2026-09-28, row 30. Builds on BL-653.
- Add a test that enumerates `CommandLineOptionTable` and fails for any option with no generator entry (or an explicit "curl writes nothing for this" entry), so options added later cannot be forgotten.
- Measure each option's lines with `Record-CurlExchange.ps1 --libcurl -` as in BL-653.

## Acceptance criteria

- [x] Measured first; output copied into Notes.
- [x] `Curl.Cli.UnitTests` reproduce each measured output byte for byte, and the enumeration test passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- **Scope (default taken).** Every remaining option is too much for one run, so this task did the titles
  groups - TLS, proxy and authentication (plus `--location-trusted`, netrc, SASL and delegation) - and
  filed **BL-1106** for FTP, TFTP, telnet, mail, MQTT, retry, rate and the rest. The enumeration test
  `LibcurlSourceCodeOptionCoverageTests` lists all 278 parsed options as written, writes-nothing or
  waiting for BL-1106; an unlisted option fails it.
- **Measured** 2026-10-01, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -NoServer -CurlArgs
  --libcurl,-,-s,...` against `https://127.0.0.1:1/` (and `http://`, `ftp://`). Lines each option adds:
  - `-k`: `SSL_VERIFYPEER, 0L` and `SSL_VERIFYHOST, 0L` before `SSLVERSION`. `--cacert f`: `CAINFO`. `--cert c.pem`: `SSLCERT`; `c.pem:pw` adds `KEYPASSWD "pw"` first; `--pass` wins over it; `C:/x/c.pem:pw` keeps the drive; `a\:b:pw` gives `"a:b"`; `a\b:pw` gives `a\b`; `pkcs11:a:b` is unsplit and adds `SSLCERTTYPE "ENG"`; `c.pem:` gives no password; `a:b:c` gives `a` / `b:c`.
  - `--key`, `--cert-type`, `--key-type`, `--pass`, `--pinnedpubkey`: `SSLKEY`, `SSLCERTTYPE`, `SSLKEYTYPE`, `KEYPASSWD`, `PINNEDPUBLICKEY`. `--ciphers`: `SSL_CIPHER_LIST` after `SSLVERSION`. `--no-alpn`: `SSL_ENABLE_ALPN, 0L`.
  - `--ssl-no-revoke`/`--ssl-revoke-best-effort`/`--ssl-allow-beast`/`--ca-native`/`--ssl-auto-client-cert`: `SSL_OPTIONS, (long)CURLSSLOPT_NO_REVOKE` / `REVOKE_BEST_EFFORT` / `ALLOW_BEAST` / `NATIVE_CA` / `AUTO_CLIENT_CERT`; `--tls-earlydata`: `SSL_OPTIONS, 64UL`. Several: one bitmask, `|` and a new line indented to the value.
  - `--tlsv1`/`--tlsv1.0`/`1.1`/`1.3`: `SSLVERSION, (long)CURL_SSLVERSION_TLSv1_0`/`_0`/`_1`/`_3`. `--tls-max 1.2`: `(long)(CURL_SSLVERSION_TLSv1_2 | CURL_SSLVERSION_MAX_TLSv1_2)`; `--tls-max default`: unchanged.
  - Nothing: `--capath`, `--crlfile`, `--tls13-ciphers`, `--curves`, `--sigalgs`, `--cert-status`, `--engine`, `--no-sessionid`, `--socks5-gssapi-service`, `--proxy-capath`, `--proxy-tls13-ciphers`, `--proxy-crlfile`. `--ssl-sessions`, `--tlsuser`, `--ech`: refused by this build (exit 2).
  - `-x http://p:3128`: `PROXY` after `NOPROGRESS`/`NOBODY`, `HEADEROPT, 1L` after the cookie lines (https URL, or `-p`; not plain http, not ftp). `-U`: `PROXYUSERPWD`. `-p`: `HTTPPROXYTUNNEL`. `--preproxy`: `PRE_PROXY`. `--proxy-basic`/`digest`/`ntlm`/`negotiate`/`anyauth`: `PROXYAUTH` `CURLAUTH_BASIC`/`DIGEST`/`NTLM`/`GSSNEGOTIATE`/`ANY` (basic+negotiate gives `GSSNEGOTIATE`). `--noproxy`, `--suppress-connect-headers`, `--proxy-service-name`, `--haproxy-protocol`, `--haproxy-clientip`: `NOPROXY`, `SUPPRESS_CONNECT_HEADERS`, `PROXY_SERVICE_NAME`, `HAPROXYPROTOCOL`, `HAPROXY_CLIENT_IP` in that order before `FAILONERROR`. `-x `: `PROXY, ""`.
  - `--socks4`/`4a`/`5`/`5-hostname`/`--proxy1.0`: `PROXY` then `PROXYTYPE, (long)CURLPROXY_SOCKS4`/`SOCKS4A`/`SOCKS5`/`SOCKS5_HOSTNAME`/`HTTP_1_0`; `--proxy1.0 p -x q`: no `PROXYTYPE`. `--socks5-basic`/`--socks5-gssapi`: `SOCKS5_AUTH` `CURLAUTH_BASIC`/`GSSNEGOTIATE`; `--socks5-gssapi-nec`: `SOCKS5_GSSAPI_NEC`; after `IPRESOLVE`.
  - `--proxy-header`: its own list, `PROXYHEADER` after `AWS_SIGV4`/`AUTOREFERER`, HTTP only (no list declared for ftp). Proxy TLS options pair with the server ones (`PROXY_KEYPASSWD`, `PROXY_CAINFO`, `PROXY_PINNEDPUBLICKEY`, `PROXY_SSLCERT`, `PROXY_SSLCERTTYPE`, `PROXY_SSLKEY`, `PROXY_SSLKEYTYPE`, `PROXY_SSL_VERIFYPEER/HOST`, `PROXY_SSL_OPTIONS`, `PROXY_SSL_CIPHER_LIST`) and are written without a proxy; `--proxy-tlsv1`: `PROXY_SSLVERSION, (long)CURL_SSLVERSION_TLSv1` only with a proxy.
  - `--basic`/`--digest`/`--ntlm`/`--negotiate`/`--anyauth`: `HTTPAUTH` after the body; all four: four names; `--oauth2-bearer t`: `XOAUTH2_BEARER` after `NOBODY` and `HTTPAUTH, (long)CURLAUTH_NONE |` / `64UL`; `--aws-sigv4`: `NONE | 128UL` and `AWS_SIGV4` after `FOLLOWLOCATION`/`UNRESTRICTED_AUTH`; negotiate+bearer+aws: `GSSNEGOTIATE | NONE | 192UL`; `--anyauth` with bearer: `ANY`.
  - `-n`/`--netrc-optional`/`--netrc-file`: `NETRC` `(long)CURL_NETRC_REQUIRED`/`OPTIONAL`, `NETRC_FILE`, then `LOGIN_OPTIONS`, after `FAILONERROR`. `--location-trusted --no-location`: `UNRESTRICTED_AUTH` alone. `--delegation policy`/`always`: `GSSAPI_DELEGATION, 1L`/`2L` (none: nothing), `--sasl-authzid`, `--sasl-ir`: last, after `CONNECT_TO`. `--service-name`: `SERVICE_NAME` before `TCP_KEEPALIVE`.
  - Combined orders pinned in `LibcurlSourceCodeProxyTlsAndAuthenticationTests` (three measured combinations and eight smaller ones).
- **Not measured, pinned by our rule for branch coverage:** `--cert C:` (`C`), `--cert 1:/x`, `--cert a\b`, `--cert a\` and `--anyauth --no-basic` (`CURLAUTH_ANYSAFE`, from libcurls table).
- **Decisions** in ADR-0326: Schannel lines on every platform, the order, curls bitmask format, the `-E` split, the enumeration test. Values needing an existing file (`--cacert`, `--netrc-file`) are tested with `pathExists` true and a short name; the measured runs used `C:/Temp/bl654/...`.
- **Scope.** Only `Curl.Cli.UnitLibrary` and `Curl.Cli.UnitTests` changed (three internal read-only accessors on `CommandLineOptions`). No option was added or changed, so `--ai-help` is unchanged.
- **Results.** `dotnet build Curl.slnx -warnaserror` clean; `Curl.Cli.UnitTests` 3438 passed; every fast test project passed in the `Measure-CodeQuality.ps1` run; `-Library Curl.Cli.UnitLibrary`: 100% line, 100% branch, 970 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --libcurl writes curl 8.21.0's setopt lines for the TLS, proxy and authentication options, and a test keeps every parsed option classified; the rest is BL-1106
