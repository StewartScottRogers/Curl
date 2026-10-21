---
id: BL-2924
title: Port upstream unit program 1658: DoH HTTPS resource record decoding
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/HttpsRecord, Curl.Conformance.UnitProgram.UnitTests/Dns/HttpsRecord, Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs, Curl.Networking.UnitLibrary/ServiceBindingRecord.cs, Curl.Networking.UnitTests/ServiceBindingRecordDecoderTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2924 — Port upstream unit program 1658: DoH HTTPS resource record decoding

## Goal

Upstream case 1658 (`doh_resp_decode_httpsrr`, the HTTPS resource record decoder) runs as a ported unit program over Curl's service-binding decoder and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2847 (too big for one run; 1658 alone is a 564-line C program, mostly test vectors). C source: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1658.c - each row decodes an RDATA blob and compares the C program's printed rendering (priority, target, ALPN list, no-def-alpn, ports, ECH, IPv4/IPv6 hints) with the expected text, or the expected error. Curl's counterpart: `Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs`, `ServiceBindingRecord.cs`, `ServiceBindingFailure.cs`. Reproduce the C program's rendering function in the port so rows compare as text. One `IUnitProgram` in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/HttpsRecord/`, registered by one added line in `UnitPrograms.Dns.cs` (union-merged); test in `Curl.Conformance.UnitProgram.UnitTests/Dns/HttpsRecord/`.

## Acceptance criteria

- [ ] `unit1658` is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/HttpsRecord/` with checks matching the C program row for row (named in its doc comment), registered in `UnitPrograms.Dns.cs`, with a test.
- [ ] Case 1658 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1658'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where the port finds Curl wrong, the fix has a focused unit test in `Curl.Networking.UnitTests` (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
