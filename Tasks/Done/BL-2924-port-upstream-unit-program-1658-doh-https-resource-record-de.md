---
id: BL-2924
title: Port upstream unit program 1658: DoH HTTPS resource record decoding
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/HttpsRecord, Curl.Conformance.UnitProgram.UnitTests/Dns/HttpsRecord, Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs, Curl.Networking.UnitLibrary/ServiceBindingRecord.cs, Curl.Networking.UnitTests/ServiceBindingRecordDecoderTests.cs, Curl.Networking.UnitLibrary/ServiceBindingFailure.cs, Curl.Networking.UnitTests/NetworkingAdversarialTests.cs, Curl.Conformance.UnitLibrary/UpstreamCaseRunner.cs, Curl.Conformance.UnitLibrary/CLAUDE.md, Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2924 — Port upstream unit program 1658: DoH HTTPS resource record decoding

## Goal

Upstream case 1658 (`doh_resp_decode_httpsrr`, the HTTPS resource record decoder) runs as a ported unit program over Curl's service-binding decoder and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2847 (too big for one run; 1658 alone is a 564-line C program, mostly test vectors). C source: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1658.c - each row decodes an RDATA blob and compares the C program's printed rendering (priority, target, ALPN list, no-def-alpn, ports, ECH, IPv4/IPv6 hints) with the expected text, or the expected error. Curl's counterpart: `Curl.Networking.UnitLibrary/ServiceBindingRecordDecoder.cs`, `ServiceBindingRecord.cs`, `ServiceBindingFailure.cs`. Reproduce the C program's rendering function in the port so rows compare as text. One `IUnitProgram` in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/HttpsRecord/`, registered by one added line in `UnitPrograms.Dns.cs` (union-merged); test in `Curl.Conformance.UnitProgram.UnitTests/Dns/HttpsRecord/`.

## Acceptance criteria

- [x] `unit1658` is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/HttpsRecord/` with checks matching the C program row for row (named in its doc comment), registered in `UnitPrograms.Dns.cs`, with a test.
- [x] Case 1658 ends `Passed` with `$env:CURL_UPSTREAM_CASE='1658'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where the port finds Curl wrong, the fix has a focused unit test in `Curl.Networking.UnitTests` (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Port: `Unit1658HttpsRecordDecoding` runs the C program's 30 rows over `ServiceBindingRecordDecoder.Decode` and renders each as `rrresults` does (curl's `CURLcode`, the target with its trailing dot, ALPN ids h1=8, h2=0x10, h3=0x20 kept once each, then no-def-alpn, port, ipv4, ech, ipv6). All 30 match.
- The port found the decoder wrong in six ways (ADR-0727, amending ADR-0312): control characters in the target (`Curl_junkscan`), keys out of order, 1-3 trailing bytes (curl ignores them), an empty `alpn` or alpn-id (curl accepts), an empty `ech` (curl refuses), and alpn overrun vs. other bad values (different codes). `ServiceBindingFailure` gained `TargetNameHasControlCharacter`, `ParametersOutOfOrder`, `BadApplicationProtocolList`; focused tests are in `ServiceBindingRecordDecoderTests`, and two `NetworkingAdversarialTests` rows that pinned the old empty-alpn refusal became an empty-ech row.
- Row "rname with null byte" is `\x04sa\x0e...` (C reads `\x00e` as one hex escape), so the label check fails, and curl returns 27 because `doh_resp_decode_httpsrr` discards `doh_decode_rdata_name`'s code; `BadTargetName` maps to 27.
- The case runner passed no arguments to a tool case with no `<command>`; `runner.pm` passes `-`, so `first.c` writes `URL: -`, which test1658's `<stderr>` expects. Fixed in `UpstreamCaseRunner.ProgramArguments`; touches widened to `ServiceBindingFailure.cs`, `NetworkingAdversarialTests.cs`, `UpstreamCaseRunner.cs`, `UpstreamCaseRunnerTests.cs` and `Curl.Conformance.UnitLibrary/CLAUDE.md`, which no task in Doing on `origin/work/dark-factory` names. test501 and test509 ignore their argument, so they still pass.
- Quality: Curl.Networking.UnitLibrary, Curl.Conformance.UnitProgram.UnitLibrary and Curl.Conformance.UnitLibrary at 100% line and branch, worst CRAP 10 (after splitting `ReadTargetName` and `AppendParameters`, which were at 12 and 14).

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: bf960bc6 1c32b31e a1753666 dd1cc079.
- 2026-10-21: Doing -> Done. unit1658 is ported over the HTTPS record decoder, which now matches curl 8.22.0's doh_resp_decode_httpsrr; case 1658 passes and is on the ratchet
