---
id: BL-2892
title: Port upstream unit programs 1664 and 1961: strparse.c and curlx_inet_ntop and curlx_inet_pton
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1664StringParsing.cs, Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1961InetAddressText.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1664StringParsingTests.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1961InetAddressTextTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.UrlParsing.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2892 — Port upstream unit programs 1664 and 1961: strparse.c and curlx_inet_ntop and curlx_inet_pton

## Goal

Upstream cases 1664 (`strparse.c` string parsing, feature `large-size`) and 1961 (`curlx_inet_ntop()` and `curlx_inet_pton()`) run as ported unit programs and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

Split from BL-2844 (2026-10-21), which ports the ten URL and address parsing unit programs and waits on this task. Follow ADR-0711: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/`, registered in `UnitPrograms.UrlParsing.cs`, reproducing every check of the C program (`https://github.com/curl/curl/tree/curl-8_22_0/tests/unit`) on the Curl type and returning `unitcheck.h`'s failure count, with an injectable function so a test can cover the failure branch. The Curl code under test is likely in Curl.Core.UnitLibrary or Curl.Networking.UnitLibrary (address text); 1664 may test C-only `curlx_str_*` helpers with no Curl counterpart, in which case record it as C-only. Where Curl's answer differs from the C table, measure real curl 8.22.0 first and fix Curl in its own library (add it to `touches`, with a focused test in its `.UnitTests`, ADR-0482); an internal type is reached through that library's `InternalsVisibleTo` for `Curl.Conformance.UnitProgram.UnitLibrary`. Run each case with `$env:CURL_UPSTREAM_CASE='<n>'`; `dotnet test Curl.Conformance.UnitTests --filter Name~NamedUpstreamCase`.

## Acceptance criteria

- [x] Each case named in the Goal has a port in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.UrlParsing.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/UrlParsing`; or the case has a Notes entry naming the C-only thing it tests.
- [x] Each ported case ends `Passed` with `CURL_UPSTREAM_CASE` set to it on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 1664 is C-only (decided by this run; the acceptance criteria allow it): it tests libcurl's internal strparse.c helpers curlx_str_word, curlx_str_until, curlx_str_quotedword, curlx_str_single, curlx_str_singlespace, curlx_str_number, curlx_str_hex, curlx_str_octal and curlx_str_newline and prints their raw return codes and cursor positions. Curl has no counterpart: its parsers read with the BCL (long.TryParse, spans) inside each library, so there is no Curl type whose answers these rows could check. It stays off PassingUpstreamCases.txt.
- 1961 is ported as Unit1961InetAddressText: curlx_inet_ntop is IPAddress.ToString() over the address's bytes (how Curl writes addresses, e.g. HaproxyProtocolHeader), curlx_inet_pton is DnsServerList.TryParseAddress (Curl's strict inet_pton reader). The C program's guard byte past each buffer becomes an exact 4- or 16-byte comparison. Curl matched every row, so no product fix was needed.
- Measured: case 1961 Passed with CURL_UPSTREAM_CASE=1961; Measure-CodeQuality on the library flagged ParseAddress's refused branch, rewritten branch-free.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 994c71ca 67867c61 aca7e1a2 dd1cc079.
- 2026-10-21: Doing -> Done. unit1961 (curlx_inet_ntop/pton) ported and on the ratchet; 1664 recorded as C-only strparse.c helpers
