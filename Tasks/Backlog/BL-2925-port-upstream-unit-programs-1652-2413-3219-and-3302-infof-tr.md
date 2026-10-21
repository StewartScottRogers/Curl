---
id: BL-2925
title: Port upstream unit programs 1652, 2413, 3219 and 3302: infof truncation, peer host parsing, protocol mask and GSSAPI delegation
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Dns.cs, Curl.Conformance.UnitProgram.UnitLibrary/Dns/Easy, Curl.Conformance.UnitProgram.UnitTests/Dns/Easy, Libcurl, Libcurl.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2925 — Port upstream unit programs 1652, 2413, 3219 and 3302: infof truncation, peer host parsing, protocol mask and GSSAPI delegation

## Goal

Upstream cases 1652, 2413, 3219 and 3302 run as ported unit programs (or have a recorded reason) and the ported ones are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2847 (too big for one run). C sources under https://github.com/curl/curl/tree/curl-8_22_0/tests/unit:
- `unit1652.c`: `Curl_infof` through `CURLOPT_DEBUGFUNCTION` with `CURLOPT_VERBOSE`: format, empty string, `(nil)` for a NULL `%s`, a 2045-character line passes whole (2046 with `\n`), longer ones are cut to 2047 characters ending `...\n`. Find where Libcurl delivers informational text to the debug callback; if it does not truncate, fix it there (feature `verbose-strings`).
- `unit2413.c`: `Curl_peer_create` over seven host strings (name, IPv4, `::1`, `[::1]`, trailing dot, `[::1%tada]`, `::1%tada`): hostname, port, IPv6 flag and zone id. Find Curl's host/zone-id parsing (URL host model in `Curl.Core.UnitLibrary`, or `Curl.Networking`).
- `unit3219.c`: `CURLPROTO_MASK` includes HTTP and GOPHERS and excludes WS, WSS and MQTTS. Port the observable part: Libcurl's protocol-bit setter (`Libcurl/Options/Network/EasyNetworkOptionSetters.cs`, `ProtocolsOf`) keeps the HTTP and GOPHERS bits and drops the WS, WSS and MQTTS bits as `CURLOPT_PROTOCOLS` does upstream; measure upstream's `setopt.c` before pinning.
- `unit3302.c`: `CURLOPT_GSSAPI_DELEGATION` stores FLAG, POLICY_FLAG, both, NONE, and `0xFF` masked to both - Curl has no `data->set`; read it back from `EasyOptions` (`GssApiDelegation`, see `Libcurl.UnitTests/Options/Authentication/EasyAuthenticationOptionSettersTests.cs`) and say so in the doc comment.
One `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Easy/`, each registered by one added line in `UnitPrograms.Dns.cs` (union-merged); tests in `Curl.Conformance.UnitProgram.UnitTests/Dns/Easy/`. Narrow `Libcurl` in `touches` to files per ADR-0694 once you know them; add the host-parsing library's file for 2413 if it needs a fix, per the lane rules.

## Acceptance criteria

- [ ] Each of `unit1652`, `unit2413`, `unit3219` and `unit3302` is ported in `Curl.Conformance.UnitProgram.UnitLibrary/Dns/Easy/` with checks matching the C program row for row (named in its doc comment), registered in `UnitPrograms.Dns.cs`, with a test; or Notes name the C-only thing a case tests and the ADR from BL-2840 records it.
- [ ] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
