---
id: BL-2890
title: Port upstream unit program 1614: noproxy and CIDR comparisons
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/Unit1614NoProxyMatching.cs, Curl.Conformance.UnitProgram.UnitTests/UrlParsing/Unit1614NoProxyMatchingTests.cs, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.UrlParsing.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2890 — Port upstream unit program 1614: noproxy and CIDR comparisons

## Goal

Upstream cases 1614 (noproxy and CIDR comparisons, `Curl_check_noproxy` and `Curl_cidr4_match`/`Curl_cidr6_match`; feature `proxy`) run as ported unit programs and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

Split from BL-2844 (2026-10-21), which ports the ten URL and address parsing unit programs and waits on this task. Follow ADR-0711: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/`, registered in `UnitPrograms.UrlParsing.cs`, reproducing every check of the C program (`https://github.com/curl/curl/tree/curl-8_22_0/tests/unit`) on the Curl type and returning `unitcheck.h`'s failure count, with an injectable function so a test can cover the failure branch. The Curl code under test is likely in Curl.Networking.UnitLibrary or Curl.Core.UnitLibrary (the `--noproxy` / `NO_PROXY` matcher). Where Curl's answer differs from the C table, measure real curl 8.22.0 first and fix Curl in its own library (add it to `touches`, with a focused test in its `.UnitTests`, ADR-0482); an internal type is reached through that library's `InternalsVisibleTo` for `Curl.Conformance.UnitProgram.UnitLibrary`. Run each case with `$env:CURL_UPSTREAM_CASE='<n>'`; `dotnet test Curl.Conformance.UnitTests --filter Name~NamedUpstreamCase`.

## Acceptance criteria

- [x] Each case named in the Goal has a port in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.UrlParsing.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/UrlParsing`; or the case has a Notes entry naming the C-only thing it tests.
- [x] Each ported case ends `Passed` with `CURL_UPSTREAM_CASE` set to it on Windows and is on `PassingUpstreamCases.txt`.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- 2026-10-21: Ported unit1614 as `UrlParsing/Unit1614NoProxyMatching` over Curl.Core's `NoProxyMatcher.Matches`; every `list4`, `list6` and `list` row (the `USE_IPV6` rows included) passes, so Curl needed no fix (criterion 3 holds with nothing to fix). Curl has no separate `cidr4_match`/`cidr6_match`, so the CIDR rows ask `Matches(address, "network/bits")`, the path `proxy_check_noproxy` takes to them (`/0` compares the whole address, as `bits == 0` does); chosen over widening `NoProxyMatcher`'s API to stay inside `touches`. Row lines are written unprefixed, as `curl_mfprintf` writes them, then the one `fail_if(err, "errors")` failure. Quality: Curl.Conformance.UnitProgram.UnitLibrary 100% line, 100% branch, 0 failing members. The matching uses no OS text or paths, so the case is platform-neutral.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 994c71ca 67867c61 aca7e1a2 dd1cc079.
- 2026-10-21: Doing -> Done. unit1614 (noproxy and CIDR comparisons) runs as a ported unit program and case 1614 is on the ratchet
