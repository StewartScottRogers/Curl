---
id: BL-2923
title: Port upstream unit programs 1650 and 1655: DoH request encoding and response parsing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh, Curl.Conformance.UnitProgram.UnitTests/Dns/Doh, Curl.Networking.UnitLibrary/DohDnsResolver.cs, Curl.Networking.UnitLibrary/DohResponseReader.cs, Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2923 — Port upstream unit programs 1650 and 1655: DoH request encoding and response parsing

## Goal

Upstream cases 1650 (DoH request encoding and response decoding) and 1655 (`doh_req_encode` name-length, label and buffer-length checks) run as ported unit programs over Curl's DoH code and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2847 (too big for one run). C sources: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1650.c (request packets byte for byte, response decode return codes and outputs, address extraction) and https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1655.c (`DOH_DNS_NAME_TOO_LONG`, `DOH_DNS_BAD_LABEL`, the 255-character name, trailing dot, buffer too small). Curl's counterparts: `Curl.Networking.UnitLibrary/DohDnsResolver.cs` (query encoding), `DohResponseReader.cs`, `DnsAnswerDecoder.cs`, `DnsAnswer.cs`. Map each C `DOHcode` to Curl's failure outcome; the canary/overflow checks of 1655 hold by construction in C# (no write past a span) - port them as "the encoder refuses a buffer too small / never writes past it" and say so in the doc comment. One `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh/`, each registered by one added line in `UnitPrograms.Dns.cs` (union-merged); tests in `Curl.Conformance.UnitProgram.UnitTests/Dns/Doh/`. If an encoder or decoder needed by a row is not reachable (internal without `InternalsVisibleTo` to this library), file that as its own task first.

## Acceptance criteria

- [ ] `unit1650` and `unit1655` are ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Doh/` with checks matching the C programs row for row (named in each doc comment), registered in `UnitPrograms.Dns.cs`, each with a test.
- [ ] Cases 1650 and 1655 end `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and are on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in `Curl.Networking.UnitTests` (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
