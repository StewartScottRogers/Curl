---
id: BL-4011
title: CGR-265 remainder: Libcurl accepts DNS_*, HTTPSIG_*, ECH, CAPATH, TLS13_CIPHERS and other options the advertised build lacks
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4029]
touches: [Libcurl/Options/Tls/EasyTlsOptionSetters.cs, Libcurl/Options/Network/EasyNetworkOptionSetters.cs, Libcurl/Options/Authentication/EasyAuthenticationOptionSetters.cs, Libcurl.UnitTests/Cgr265RemainderTests.cs, Libcurl.UnitTests/Options/Tls/TlsPeerOptionSettersTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-29
---
# BL-4011 — CGR-265 remainder: Libcurl accepts DNS_*, HTTPSIG_*, ECH, CAPATH, TLS13_CIPHERS and other options the advertised build lacks

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-28: Created.

## Notes

- Split from BL-3794, which fixed only TLSAUTH_* (CURLE_NOT_BUILT_IN on Windows via `EasyAuthenticationOptionSetters.SrpIsBuiltIn`).
- Left: DNS_*, ECH, CAPATH, CRLFILE, ISSUERCERT (+PROXY_), TLS13_CIPHERS, SSL_EC_CURVES, SSL_SIGNATURE_ALGORITHMS, SSL_VERIFYSTATUS, HTTP_VERSION 3/4/5/30/31, STREAM_WEIGHT, TCP_FASTOPEN, KRBLEVEL, PROXYTYPE=8, Linux SSLENGINE.
- HTTPSIG_* returning UNKNOWN_OPTION was tried and reverted: `Curl.Conformance.LibTest.UnitLibrary` Lib5000/Lib5004 set them and expect 0, so that change needs Curl.Conformance.LibTest.* in touches and those cases adjusted (curl 8.22.0 lacks them; upstream master has them).
- Setting an option with a SetOption unit test needs the per-platform answer pinned (OSCondition) or an injectable flag like `SrpIsBuiltIn`; 100% branch coverage is measured on Windows only.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-28 (lane 5): Measured curl 8.22.0 Schannel through the tool (`curl <opt> http://127.0.0.1:1/`, warnings read without `-s`). libcurl answers NOT_BUILT_IN for `PROXY_CAPATH` ("ignoring setting the CA path for the proxy"), `TLS13_CIPHERS` and `PROXY_TLS13_CIPHERS` ("ignoring --tls13-ciphers, not supported by libcurl with Schannel"). `--capath` alone gives only the proxy warning, so `CAPATH` itself appears to answer OK. The tool takes `--curves`, `--sigalgs`, `--cert-status`, `--doh-cert-status`, `--tcp-fastopen` and `--engine` with no warning, so the tool cannot show libcurl's answer for those; they need a libcurl-level measurement. The tool itself rejects `--crlfile`, `--proxy-crlfile`, `--proxy-issuercert`, `--ech`, `--dns-*`, `--http3` and `--http3-only` (exit 2), so it never calls setopt for them. `--krb` is deprecated and does nothing.
- Requeued for BL-4029 before any edit. Gating `PROXY_CAPATH`/`TLS13_CIPHERS` in Libcurl alone would break the curl tool on Windows: `ToolEasyHandle.SetOptions` fails the transfer on any setopt error, and `--capath` always sets `PROXY_CAPATH` too. Plan for the next claim: add `Tls13CiphersIsBuiltIn` and `ProxyCaPathIsBuiltIn` (default `!OperatingSystem.IsWindows()`, like `SrpIsBuiltIn`) in `EasyTlsOptionSetters`. Move the Capath/Tls13Ciphers twin tests out of `TlsPeerOptionSettersTests` (added to touches) into the `[DoNotParallelize]` `Cgr265RemainderTests`, with both flag values. Then write an ADR.
- 2026-10-28: Doing -> Backlog. Waits for BL-4029: the curl tool must tolerate CURLE_NOT_BUILT_IN for --capath/--tls13-ciphers before Libcurl gates PROXY_CAPATH and TLS13_CIPHERS on Windows
- 2026-10-29: Backlog -> Doing. Criteria at claim: fbecace0.
- 2026-10-29 (lane 6): Did the plan above. `EasyTlsOptionSetters` gates `TLS13_CIPHERS`, `PROXY_TLS13_CIPHERS` (`Tls13CiphersIsBuiltIn`) and `PROXY_CAPATH` (`ProxyCaPathIsBuiltIn`), both `!OperatingSystem.IsWindows()`; off they answer NOT_BUILT_IN for every value, NULL included, as `setopt.c`'s `Curl_ssl_supports` check does. `CAPATH` stays OK, as measured. ADR-0991. The Capath/Tls13Ciphers twin tests moved from `TlsPeerOptionSettersTests` into the `[DoNotParallelize]` `Cgr265RemainderTests`, which pins both flag values, so the tests are platform-neutral.
- The placeholder acceptance criterion was ticked with the build clean, the fast tests green (Libcurl.UnitTests 1513 passed, Curl.Console.UnitTests 3042 passed, no failures in any project) and `Measure-CodeQuality.ps1 -Library Libcurl` reporting 0 failing members.
- The options still left (DNS_*, ECH, CRLFILE, ISSUERCERT, curves, sigalgs, VERIFYSTATUS, HTTP_VERSION 3+, STREAM_WEIGHT, TCP_FASTOPEN, KRBLEVEL, PROXYTYPE 8, Linux SSLENGINE, HTTPSIG_*) need a libcurl-level measurement the tool cannot give; filed as BL-4066. Touches were not changed: EasyNetworkOptionSetters, EasyAuthenticationOptionSetters and Cgr265RemainderTests' other cases were not needed.
- 2026-10-29: Doing -> Done. Libcurl answers CURLE_NOT_BUILT_IN for TLS13_CIPHERS, PROXY_TLS13_CIPHERS and PROXY_CAPATH on the Schannel build (ADR-0991); the rest is BL-4066
