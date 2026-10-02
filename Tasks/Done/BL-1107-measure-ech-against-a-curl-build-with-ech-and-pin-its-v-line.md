---
id: BL-1107
title: Measure --ech against a curl build with ECH and pin its -v lines and exit 101 text
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-711]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1107 — Measure --ech against a curl build with ECH and pin its -v lines and exit 101 text

## Goal

Under `-v`, `--ech` writes curl 8.21.0's `* ECH: ...` information lines, and a rejected offer's exit 101 prints the OpenSSL build's own error text. Both are measured with a curl build that has ECH.

## Context

- ADR-0327 (BL-711) took these from source. No build on the machine had ECH: the mingw Schannel 8.21.0 build and WSL's OpenSSL 8.18.0 build show no `ECH` in `curl -V`. Today exit 101 prints `ECH attempted but failed` (`TlsFailureMessages.EchRequired`), and no `ECH:` line is written.
- Build or get a curl with ECH (OpenSSL 4.0 or BoringSSL), for example in WSL. Then run `Record-CurlExchange.ps1 -Tls -k` with `--ech grease|true|hard` and `ecl:`/`pn:` against a server without ECH, with and without `--doh-url`. Copy stderr and the exit code into Notes. Also check the DoH HTTPS query bytes BL-707 pinned.

## Acceptance criteria

- [x] The measurements are in Notes.
- [x] `Curl.Networking.UnitTests` pin each mode's `-v` ECH lines and the exit 101 text as measured.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### The ECH build (2026-10-02)

No ECH curl existed on the machine, so one was built in Docker (`curl-ech:8.21.0`, image local only):
`alpine:latest`, `apk add build-base perl linux-headers wget zlib-dev`, then
OpenSSL 4.0.0 (`./Configure --prefix=/opt/ossl --libdir=lib no-docs no-tests`) and
curl 8.21.0 (`./configure --with-openssl=/opt/ossl --enable-ech --enable-httpsrr --without-libpsl --disable-docs LDFLAGS=-Wl,-rpath,/opt/ossl/lib`).
`curl -V`: `curl 8.21.0 (x86_64-pc-linux-musl) libcurl/8.21.0 OpenSSL/4.0.0 zlib/1.3.2`, Features `... ECH ... HTTPSRR ...`.
Curl's Windows-side recorder cannot reach into the container, so the server was `openssl s_server -www`
(OpenSSL 4.0.0, RSA 2048 self-signed `CN=localhost`) inside the container. Port 8443 had no ECH, and
9443 had `-ech_key` from `openssl ech -public_name example.com`. Every run was `curl -sS -v -k <args> https://localhost:<port>/`.
Lines below are the `ECH`, `curl:` and key TLS lines from stderr.

| Args (port 8443, no ECH) | `ECH:` lines in order | Exit |
| --- | --- | --- |
| (none) | after `SSL connection using`: `ECH: result: status is not attempted` (every handshake in an ECH build) | 0 |
| `--ech false` | after the handshake: `ECH: result: status is not attempted` | 0 |
| `--ech grease` (also with `ecl:`/`pn:`, also `--tls-max 1.2`) | `ECH: will GREASE ClientHello`; after the handshake: `ECH: result: status is sent GREASE, inner is NULL, outer is NULL` | 0 |
| `--ech true` (also with `pn:`, `--tlsv1.3`) | `ECH: requested but no ECHConfig available`; after the handshake: `ECH: result: status is not configured, inner is NULL, outer is NULL` | 0 |
| `--ech hard` / `--ech pn:other.example` / `--ech false --ech pn:x.example` | `ECH: requested but no ECHConfig available`; `curl: (35) SSL connect error` | 35 |
| `--ech ecl:<list>` / `true`+`ecl:` / `hard`+`ecl:` | `ECH: ECHConfig from command line`, then `TLSv1.3 (OUT), TLS alert, ECH required (633)`, `ECH: no retry_configs (rv = 1)`, `ECH required: error:0A0001A8:SSL routines::ech required`; `curl: (101) ECH required: error:0A0001A8:SSL routines::ech required` | 101 |
| `hard`+`ecl:`+`pn:other.example` | as above, with `ECH: inner: 'localhost', outer: 'other.example'` after the command-line line | 101 |
| `true`+`ecl:notbase64` (or `ecl:AAA=`) | `ECH: SSL_ECH_set1_ech_config_list failed`, `ECH: ECHConfig from command line`; after the handshake: `ECH: result: status is not configured, inner is NULL, outer is NULL` | 0 |
| `hard`+`ecl:notbase64` / `ecl:notbase64` / `false`+`ecl:notbase64` | `ECH: SSL_ECH_set1_ech_config_list failed`; `curl: (35) SSL connect error` | 35 |
| `hard` or `true` + `ecl:<list>` + `--tls-max 1.2` | `ECH: ECHConfig from command line`, `TLSv1.3 (OUT), TLS alert, protocol version (582)`; `curl: (35) TLS connect error: error:0A0000BF:SSL routines::no protocols available` | 35 |
| `--ech bogus` (with or without `ecl:`) | `curl: (43) setopt 0x2855 got bad argument` | 43 |

| Args (port 9443, ECH key) | `ECH:` lines | Exit |
| --- | --- | --- |
| `true`+`ecl:<list>` | `ECH: ECHConfig from command line`; after: `ECH: result: status is bad name (tolerated without peer verification), inner is localhost, outer is example.com` | 0 |
| `hard`+`ecl:`+`pn:other.example` | `ECH: ECHConfig from command line`, `ECH: inner: 'localhost', outer: 'other.example'`; after: `... bad name (tolerated without peer verification), inner is localhost, outer is other.example` | 0 |
| `grease` | `ECH: will GREASE ClientHello`; after: `ECH: result: status is sent GREASE, got retry-configs, inner is NULL, outer is NULL`, `ECH: retry_configs <list>`, `ECH: retry_configs for NULL from NULL, 0 3` | 0 |

Every run also printed `HTTPS-RR: -` after the resolve lines. That comes from `--enable-httpsrr` and is not specific to ECH.
The DoH path (`--doh-url`) was not measured: the recorder has no DoH server that serves HTTPS records. Its lines
(`ECH: ECHConfig from HTTPS RR`, `ECH: imported ECHConfigList of length N`, `ECH: SSL_set1_ech_config_list failed`)
come from `lib/vtls/openssl.c` and are filed as BL-1173, together with checking BL-707's query bytes.

### What changed (ADR-0359, amends ADR-0327)

- `EchModes.Of`: `pn:` or `ecl:` with no mode or with `false` is `Mandatory` (libcurl's `setopt_ech`, measured).
- `EchOffer.InfoLines` holds the setup lines above. `HandBuiltTlsProvider` writes them before the hello.
- A usable list below TLS 1.3 is exit 35 with OpenSSL's `no protocols available`, under `true` too. The `protocol version` alert record OpenSSL writes is not sent.
- A rejection writes `ECH: no retry_configs (rv = 1)`. Exit 101's text is now OpenSSL's `ECH required: error:0A0001A8:SSL routines::ech required` on every platform. `CurlEasyErrorText` keeps libcurl's `ECH attempted but failed`, because that is `curl_easy_strerror`'s text.
- Not done here, filed instead:
  - BL-1170: the post-handshake `ECH: result:` line. It needs a `TlsHandshakeEvent` field in Abstractions and Output.
  - BL-1171: the `retry_configs` lines.
  - BL-1172: exit 43 for an unknown mode, in Cli.
  - BL-1173: the DoH lines.
- Decided: the `status is not attempted` and `HTTPS-RR: -` lines are not written. The platform builds Curl matches have no ECH and do not print them.
- Coverage: the first `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` run reported 100/100 with 0 failing members. A second run flagged line 54 of `UdpChannelOpener.OpenFrom` (87.5%). That code is untouched by this task and its coverage depends on timing.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --ech writes curl's measured ECH: setup lines and exit 101 OpenSSL's ech required text; pn:/ecl: alone is hard
