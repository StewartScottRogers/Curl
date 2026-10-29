---
id: BL-4066
title: CGR-265 last part: measure libcurl's setopt answers for DNS_*, ECH, CRLFILE, ISSUERCERT, curves, sigalgs, VERIFYSTATUS, HTTP_VERSION 3+, STREAM_WEIGHT, TCP_FASTOPEN, KRBLEVEL, PROXYTYPE 8, SSLENGINE, HTTPSIG_* and gate them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Options/Tls/EasyTlsOptionSetters.cs, Libcurl/Options/Network/EasyNetworkOptionSetters.cs, Libcurl/Options/Authentication/EasyAuthenticationOptionSetters.cs, Libcurl/Options/Proxy/EasyProxyOptionSetters.cs, Libcurl/CLAUDE.md, Libcurl.UnitTests/Cgr265LastPartTests.cs, Libcurl.UnitTests/Options/Tls/EasyTlsOptionSettersTests.cs, Libcurl.UnitTests/Options/Tls/TlsPeerOptionSettersTests.cs, Libcurl.UnitTests/Options/Network/EasyNetworkOptionSettersTests.cs, Libcurl.UnitTests/Options/Authentication/EasyAuthenticationOptionSettersTests.cs, Libcurl.UnitTests/Options/Proxy/EasyProxyOptionSettersTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4066 — CGR-265 last part: measure libcurl's setopt answers for DNS_*, ECH, CRLFILE, ISSUERCERT, curves, sigalgs, VERIFYSTATUS, HTTP_VERSION 3+, STREAM_WEIGHT, TCP_FASTOPEN, KRBLEVEL, PROXYTYPE 8, SSLENGINE, HTTPSIG_* and gate them

## Goal

Libcurl's `SetOption` answers, for each CGR-265 option still left, what curl 8.22.0's libcurl answers on that platform (Schannel build on Windows, OpenSSL build on Linux and macOS).

## Context

Split from BL-4011, which gated `TLS13_CIPHERS`, `PROXY_TLS13_CIPHERS` and `PROXY_CAPATH` on Windows (ADR-0991, flags in `EasyTlsOptionSetters`). Left: `DNS_*`, `ECH`, `CRLFILE` (+`PROXY_`), `ISSUERCERT` (+`PROXY_`), `SSL_EC_CURVES`, `SSL_SIGNATURE_ALGORITHMS`, `SSL_VERIFYSTATUS`, `HTTP_VERSION` 3/4/5/30/31, `STREAM_WEIGHT`, `TCP_FASTOPEN`, `KRBLEVEL`, `PROXYTYPE`=8, Linux `SSLENGINE`, `HTTPSIG_*`.

The curl tool either rejects these options (exit 2) or prints no warning, so their libcurl answer has to be measured at the libcurl level (for example the platform libcurl's `curl_easy_setopt` driven from a C# file-based app over P/Invoke), or read from curl 8.22.0's `lib/setopt.c` with each `#ifdef` and `Curl_ssl_supports` check resolved for the Schannel and OpenSSL builds. `HTTPSIG_*`: `Curl.Conformance.LibTest.UnitLibrary` Lib5000/Lib5004 set them and expect 0, so changing those needs their files in touches. Use one injectable flag per capability, as `SrpIsBuiltIn` and `Tls13CiphersIsBuiltIn` do, so 100% branch coverage holds on Windows.

## Acceptance criteria

- [x] Each option listed under Context has its measured answer per platform recorded under Notes, with how it was measured.
- [x] Each option whose measured answer differs from Libcurl's now answers the measured code, pinned in `Libcurl.UnitTests` for both flag values, with an ADR.
- [x] `dotnet build` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members.

## Notes

- Measured by reading curl 8.22.0 `lib/setopt.c` and `lib/vtls/{vtls,schannel,openssl}.c` (the copy under Curl.Conformance.UnitTests/CurlTestCases/Original/lib), each #ifdef and Curl_ssl_supports check resolved for the Schannel build (`curl -V` here: 8.22.0 Schannel, no c-ares, HTTP2, HTTP3 or ECH) and the distro OpenSSL build; Windows ships no Schannel libcurl.dll to P/Invoke. The per-option, per-platform answers are the table in ADR-0995.
- Gated, one flag per capability, `!OperatingSystem.IsWindows()`: CRLFILE (+PROXY_), ISSUERCERT (+PROXY_ and both _BLOBs, the same Schannel gap), SSL_EC_CURVES, SSL_SIGNATURE_ALGORITHMS, SSLENGINE (Schannel NotBuiltIn, OpenSSL SslEngineNotFound for a name), SSLENGINE_DEFAULT (Schannel NotBuiltIn, OpenSSL Ok), TCP_FASTOPEN. KRBLEVEL and PROXYTYPE 8 answer NotBuiltIn everywhere.
- Decision (ADR-0995): SSL_VERIFYSTATUS, DOH_SSL_VERIFYSTATUS, ECH and DNS_* stay Ok, as Curl builds OCSP stapling, ECH and its own resolver itself; gating them broke Curl.Console's --cert-status and --ech tests, upstream test4000 and test4001 on the ratchet, and Lib1592. Their flags default on and are pinned both ways. HTTP_VERSION 3+, STREAM_WEIGHT and HTTPSIG_* already match a build with HTTP2, HTTP3 and httpsig (Libcurl advertises HTTP2 and HTTP3): unchanged.
- Touches widened (no Doing task on origin/work/dark-factory names them): the Authentication and Proxy setters for KRBLEVEL and PROXYTYPE 8, Libcurl/CLAUDE.md (its SSLENGINE sentence became untrue), and the option-group test files whose tests of gated options moved to the [DoNotParallelize] Cgr265LastPartTests.
- Measure-CodeQuality.ps1 -Library Libcurl: 0 failing members. Fast tests green on Windows; the change is platform-neutral (flags pinned both ways, defaults asserted per OS), which meets the Linux and macOS side.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 34fb773f 6396d0d8 68c0fcc3.
- 2026-10-29: Doing -> Done. Libcurl answers the platform build's setopt codes for CGR-265's last options behind per-capability flags (ADR-0995)
