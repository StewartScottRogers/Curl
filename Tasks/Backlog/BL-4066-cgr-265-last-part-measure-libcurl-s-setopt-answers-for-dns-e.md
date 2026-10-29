---
id: BL-4066
title: CGR-265 last part: measure libcurl's setopt answers for DNS_*, ECH, CRLFILE, ISSUERCERT, curves, sigalgs, VERIFYSTATUS, HTTP_VERSION 3+, STREAM_WEIGHT, TCP_FASTOPEN, KRBLEVEL, PROXYTYPE 8, SSLENGINE, HTTPSIG_* and gate them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Options/Tls/EasyTlsOptionSetters.cs, Libcurl/Options/Network/EasyNetworkOptionSetters.cs, Libcurl.UnitTests/Cgr265LastPartTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4066 — CGR-265 last part: measure libcurl's setopt answers for DNS_*, ECH, CRLFILE, ISSUERCERT, curves, sigalgs, VERIFYSTATUS, HTTP_VERSION 3+, STREAM_WEIGHT, TCP_FASTOPEN, KRBLEVEL, PROXYTYPE 8, SSLENGINE, HTTPSIG_* and gate them

## Goal

Libcurl's `SetOption` answers, for each CGR-265 option still left, what curl 8.22.0's libcurl answers on that platform (Schannel build on Windows, OpenSSL build on Linux and macOS).

## Context

Split from BL-4011, which gated `TLS13_CIPHERS`, `PROXY_TLS13_CIPHERS` and `PROXY_CAPATH` on Windows (ADR-0991, flags in `EasyTlsOptionSetters`). Left: `DNS_*`, `ECH`, `CRLFILE` (+`PROXY_`), `ISSUERCERT` (+`PROXY_`), `SSL_EC_CURVES`, `SSL_SIGNATURE_ALGORITHMS`, `SSL_VERIFYSTATUS`, `HTTP_VERSION` 3/4/5/30/31, `STREAM_WEIGHT`, `TCP_FASTOPEN`, `KRBLEVEL`, `PROXYTYPE`=8, Linux `SSLENGINE`, `HTTPSIG_*`.

The curl tool either rejects these options (exit 2) or prints no warning, so their libcurl answer has to be measured at the libcurl level (for example the platform libcurl's `curl_easy_setopt` driven from a C# file-based app over P/Invoke), or read from curl 8.22.0's `lib/setopt.c` with each `#ifdef` and `Curl_ssl_supports` check resolved for the Schannel and OpenSSL builds. `HTTPSIG_*`: `Curl.Conformance.LibTest.UnitLibrary` Lib5000/Lib5004 set them and expect 0, so changing those needs their files in touches. Use one injectable flag per capability, as `SrpIsBuiltIn` and `Tls13CiphersIsBuiltIn` do, so 100% branch coverage holds on Windows.

## Acceptance criteria

- [ ] Each option listed under Context has its measured answer per platform recorded under Notes, with how it was measured.
- [ ] Each option whose measured answer differs from Libcurl's now answers the measured code, pinned in `Libcurl.UnitTests` for both flag values, with an ADR.
- [ ] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members.

## Notes

## Log

- 2026-10-29: Created.
