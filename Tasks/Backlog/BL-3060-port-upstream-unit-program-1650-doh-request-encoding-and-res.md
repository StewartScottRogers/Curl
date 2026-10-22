---
id: BL-3060
title: Port upstream unit program 1650: DoH request encoding and response decoding
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh/Unit1650DohEncodingAndDecoding.cs, Curl.Conformance.UnitProgram.UnitTests/Dns/Doh/Unit1650DohEncodingAndDecodingTests.cs, Curl.Networking.UnitLibrary/DnsAnswerReader.cs, Curl.Networking.UnitTests/DohRequestEncodingTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3060 — Port upstream unit program 1650: DoH request encoding and response decoding

## Goal

Upstream case 1650 runs as a ported unit program over Curl's DoH encoder and decoder and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2923 (which ported unit1655 as `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh/Unit1655DohRequestEncoding.cs`; follow its shape). C source: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1650.c (request packets byte for byte, response decode return codes and outputs, address extraction). Curl's counterparts: `DnsQueryEncoder.Encode` and `DnsAnswerDecoder.Decode` in `Curl.Networking.UnitLibrary`. BL-2923's first pass found request rows and resp rows 0-2, 4-10 and 12 matched; BL-2969 fixed rows 3 and 13 (NXDOMAIN, TTL cap). Row 11 (self-pointing CNAME, curl 8.22.0 `DOH_OK`) was given to BL-2970, which is Deferred: check row 11 first and, if Curl still answers `LabelLoop`, add BL-2970 to `depends-on` and requeue. Register by one added line in `UnitPrograms.Dns.cs`.

## Acceptance criteria

- [ ] `unit1650` is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh/` with checks matching the C program row for row (named in its doc comment), registered in `UnitPrograms.Dns.cs`, with a test.
- [ ] Case 1650 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1650'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where the port finds Curl wrong, the fix has a focused unit test in `Curl.Networking.UnitTests` (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-22: Created.
