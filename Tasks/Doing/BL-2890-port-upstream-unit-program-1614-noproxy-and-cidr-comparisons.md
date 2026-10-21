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
completed:
---
# BL-2890 — Port upstream unit program 1614: noproxy and CIDR comparisons

## Goal

Upstream cases 1614 (noproxy and CIDR comparisons, `Curl_check_noproxy` and `Curl_cidr4_match`/`Curl_cidr6_match`; feature `proxy`) run as ported unit programs and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

Split from BL-2844 (2026-10-21), which ports the ten URL and address parsing unit programs and waits on this task. Follow ADR-0711: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing/`, registered in `UnitPrograms.UrlParsing.cs`, reproducing every check of the C program (`https://github.com/curl/curl/tree/curl-8_22_0/tests/unit`) on the Curl type and returning `unitcheck.h`'s failure count, with an injectable function so a test can cover the failure branch. The Curl code under test is likely in Curl.Networking.UnitLibrary or Curl.Core.UnitLibrary (the `--noproxy` / `NO_PROXY` matcher). Where Curl's answer differs from the C table, measure real curl 8.22.0 first and fix Curl in its own library (add it to `touches`, with a focused test in its `.UnitTests`, ADR-0482); an internal type is reached through that library's `InternalsVisibleTo` for `Curl.Conformance.UnitProgram.UnitLibrary`. Run each case with `$env:CURL_UPSTREAM_CASE='<n>'`; `dotnet test Curl.Conformance.UnitTests --filter Name~NamedUpstreamCase`.

## Acceptance criteria

- [ ] Each case named in the Goal has a port in `Curl.Conformance.UnitProgram.UnitLibrary/UrlParsing` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.UrlParsing.cs`, with a test in `Curl.Conformance.UnitProgram.UnitTests/UrlParsing`; or the case has a Notes entry naming the C-only thing it tests.
- [ ] Each ported case ends `Passed` with `CURL_UPSTREAM_CASE` set to it on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 994c71ca 67867c61 aca7e1a2 dd1cc079.
