---
id: BL-2847
title: Port upstream unit programs for DoH, DNS shuffling, peers and TLS settings: cases 1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303 and 3304
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns, Curl.Conformance.UnitProgram.UnitTests/Dns]
requirement: none
created: 2026-10-19
completed:
---
# BL-2847 — Port upstream unit programs for DoH, DNS shuffling, peers and TLS settings: cases 1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303 and 3304

## Goal

Upstream cases 1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303 and 3304 run as ported unit programs over Curl's DoH, DNS-shuffle, peer, protocol-mask and TLS-setting code and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

These ten `unittest` cases (skipped today with `Curl lacks the feature unittest`; gap scorecard 2026-10-19_1329: `excluded | debug-build-only`) test one area each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit): 1608 DNS shuffling (feature `shuffle-dns`), 1650 DoH parsing, 1655 `doh_req_encode` and 1658 `doh_resp_decode_httpsrr` (feature `DoH`), 1652 `infof` (feature `verbose-strings`), 2413 `Curl_peer`, 3219 `CURLPROTO_MASK` checks, 3302 `CURLOPT_GSSAPI_DELEGATION` stores its flags, 3303 connection reuse distinguishes mTLS key, cert type, key type and key password, 3304 the TLS session cache peer key uses the mTLS key, key type and cert type. Curl's counterparts: `Curl.Networking.UnitLibrary` (`DohDnsResolver`, `DohResponseReader`, name shuffling), `Curl.Tls.UnitLibrary` (session cache), `Curl.Engine.UnitLibrary` and `Curl.Api.UnitLibrary` (options, connection reuse, protocol masks); find each with the Glossary and `rg`. 1608 needs `shuffle-dns`, which `UpstreamCurlPlatform` keeps off (a debug-time feature): a port reports it for that case only. Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program and returning its failure count. 3302, 3303 and 3304 test where libcurl stores an option in its `data->set` struct; if Curl has no such struct, port the observable part (the option reaches the transfer, connection reuse and session key differ) and say so in Notes.

## Acceptance criteria

- [ ] Each of the ten cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`; or the case has a Notes entry naming the C-only thing it tests and the ADR from BL-2840 row says so.
- [ ] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches` in the same commit.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

Ports go in Curl.Conformance.UnitProgram.UnitLibrary/Dns/ and their tests in Curl.Conformance.UnitProgram.UnitTests/Dns/; register them only in Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, which BL-2886 creates. Its project references are added by BL-2886; if one is missing, add it in a separate one-line commit and say so in the Log (Stewart, 2026-10-21: keep the seven ports parallel).

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 759c7e24 2e3e75ab e66ae9dd c0723528 620ebead.
