---
id: BL-4011
title: CGR-265 remainder: Libcurl accepts DNS_*, HTTPSIG_*, ECH, CAPATH, TLS13_CIPHERS and other options the advertised build lacks
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Options/Tls/EasyTlsOptionSetters.cs, Libcurl/Options/Network/EasyNetworkOptionSetters.cs, Libcurl/Options/Authentication/EasyAuthenticationOptionSetters.cs, Libcurl.UnitTests/Cgr265RemainderTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4011 — CGR-265 remainder: Libcurl accepts DNS_*, HTTPSIG_*, ECH, CAPATH, TLS13_CIPHERS and other options the advertised build lacks

## Goal

<!-- One sentence: the observable outcome once this task is done. -->

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [ ] <!-- A statement someone else can check from the repository without asking a question. -->

## Notes

## Log

- 2026-10-28: Created.

## Notes

- Split from BL-3794, which fixed only TLSAUTH_* (CURLE_NOT_BUILT_IN on Windows via `EasyAuthenticationOptionSetters.SrpIsBuiltIn`).
- Left: DNS_*, ECH, CAPATH, CRLFILE, ISSUERCERT (+PROXY_), TLS13_CIPHERS, SSL_EC_CURVES, SSL_SIGNATURE_ALGORITHMS, SSL_VERIFYSTATUS, HTTP_VERSION 3/4/5/30/31, STREAM_WEIGHT, TCP_FASTOPEN, KRBLEVEL, PROXYTYPE=8, Linux SSLENGINE.
- HTTPSIG_* returning UNKNOWN_OPTION was tried and reverted: `Curl.Conformance.LibTest.UnitLibrary` Lib5000/Lib5004 set them and expect 0, so that change needs Curl.Conformance.LibTest.* in touches and those cases adjusted (curl 8.22.0 lacks them; upstream master has them).
- Setting an option with a SetOption unit test needs the per-platform answer pinned (OSCondition) or an injectable flag like `SrpIsBuiltIn`; 100% branch coverage is measured on Windows only.
- 2026-10-28: Backlog -> Doing. Criteria at claim: fbecace0.
