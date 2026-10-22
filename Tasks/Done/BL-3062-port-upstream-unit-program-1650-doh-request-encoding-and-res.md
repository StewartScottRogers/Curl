---
id: BL-3062
title: Port upstream unit program 1650: DoH request encoding and response decoding
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh/Unit1650DohEncodingAndDecoding.cs, Curl.Conformance.UnitProgram.UnitTests/Dns/Doh/Unit1650DohEncodingAndDecodingTests.cs, Curl.Networking.UnitLibrary/DnsAnswerReader.cs, Curl.Networking.UnitTests/DohRequestEncodingTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3062 — Port upstream unit program 1650: DoH request encoding and response decoding

## Goal

Upstream case 1650 runs as a ported unit program over Curl's DoH encoder and decoder and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2923 (which ported unit1655 as `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh/Unit1655DohRequestEncoding.cs`; follow its shape). C source: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1650.c (request packets byte for byte, response decode return codes and outputs, address extraction). Curl's counterparts: `DnsQueryEncoder.Encode` and `DnsAnswerDecoder.Decode` in `Curl.Networking.UnitLibrary`. BL-2923's first pass found request rows and resp rows 0-2, 4-10 and 12 matched; BL-2969 fixed rows 3 and 13 (NXDOMAIN, TTL cap). Row 11 (self-pointing CNAME, curl 8.22.0 `DOH_OK`) was given to BL-2970, which is Deferred: check row 11 first and, if Curl still answers `LabelLoop`, add BL-2970 to `depends-on` and requeue. Register by one added line in `UnitPrograms.Dns.cs`.

## Acceptance criteria

- [x] `unit1650` is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh/` with checks matching the C program row for row (named in its doc comment), registered in `UnitPrograms.Dns.cs`, with a test.
- [x] Case 1650 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1650'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where the port finds Curl wrong, the fix has a focused unit test in `Curl.Networking.UnitTests` (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-22, lane 1. Row 11 checked first: BL-2970's decoder change landed as BL-3011 (Done), so the
  self-pointing CNAME decodes `None` and BL-2970 is not a dependency. Ported as
  `Unit1650DohEncodingAndDecoding` (116 checks: 3 request rows byte for byte, 14 response rows, 49
  prefixes and 49 suffixes of `full49`, and the whole `full49`), with the encoder and decoder injected
  so its test counts the checks a wrong codec fails. Every row matched Curl as it is: no Curl fix, so no
  `Curl.Networking` change (the third criterion holds vacuously). Choice: the C program's `abort_if`
  stops at the first mismatch; the port, like unit1655's, runs every check and counts the failures,
  since the case only checks the exit code. Case 1650 passes by name and is on the ratchet; it prints
  nothing platform-dependent. `Curl.Conformance.UnitProgram.UnitLibrary`: 100% line and branch,
  complexity at most 10, 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: ec1a9147 dc9e6844 a1753666 8e1ce02b.
- 2026-10-22: Doing -> Done. unit1650 ported over DnsQueryEncoder and DnsAnswerDecoder; case 1650 passes and is on the ratchet
