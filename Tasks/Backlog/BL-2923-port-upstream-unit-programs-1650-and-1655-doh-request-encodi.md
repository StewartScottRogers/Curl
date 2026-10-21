---
id: BL-2923
title: Port upstream unit programs 1650 and 1655: DoH request encoding and response parsing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2967]
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

- 2026-10-21 (lane 1, first pass, no code written): read unit1650.c, unit1655.c and curl 8.22.0's `lib/vdns/doh.c` against Curl's codec.
  - unit1655 needs no production change. The encoder is `DnsQueryEncoder.Encode` (not `DohDnsResolver`), which allocates exactly the query's length, so the port emulates `doh_req_encode(name, type, buffer, len, &olen)` as: Encode's failure if any, else `TooSmall` (curl's `DOH_TOO_SMALL_BUFFER`) when `len` is below the bytes' length, else `olen` = that length. The canary rows become "the 255-character `max` encodes to 272 bytes, one past the 271-byte `dohbuffer`" and "every refused name writes nothing". Checked by hand: toolong (256 with trailing dot) gives 273 > 272, `NameTooLong`; the empty and 64-byte labels give `BadLabel`; `a.com` and `a.com.` both 23, `aa.com` 24.
  - unit1650's three request rows and resp rows 0-2, 4-10 and 12 match Curl's decoder today. Three rows do not, because Curl's decoder is curl 8.21.0's: row 3 (RCODE 3, curl 8.22.0 `DOH_DNS_NXDOMAIN`, Curl `BadRcode`), row 11 (self-pointing CNAME, 8.22.0 `DOH_OK` since it no longer parses CNAME data, Curl `LabelLoop`) and row 13 (TTL capped at 86400 in 8.22.0, Curl 16776999). Fixing them changes `DnsMessageFailure`, `DohQueryResult`, `DohTraceLines` and `DnsServerQuery`, outside this task's touches, and needs real-curl measurement of the trace lines: filed as BL-2967 and added to `depends-on`. Once it is Done, port both programs as planned above.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 3b68dec0 42440f53 52e5bf94 dd1cc079.
- 2026-10-21: Doing -> Backlog. Waits on BL-2967: unit1650 rows 3, 11 and 13 need Curl's DoH decoder to match curl 8.22.0 (NXDOMAIN, CNAME data skipped, TTL cap)
