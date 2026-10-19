---
id: BL-2842
title: Port upstream unit programs for hashes, encodings and signing: cases 1302, 1600, 1601, 1610, 1611, 1612, 1615, 1979 and 1980
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2841]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary, Curl.Conformance.UnitProgram.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2842 — Port upstream unit programs for hashes, encodings and signing: cases 1302, 1600, 1601, 1610, 1611, 1612, 1615, 1979 and 1980

## Goal

Upstream cases 1302, 1600, 1601, 1610, 1611, 1612, 1615, 1979 and 1980 run as ported unit programs over Curl's own implementations and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

These nine `unittest` cases (skipped today with `Curl lacks the feature unittest`; gap scorecard 2026-10-19_1329 has them `excluded | debug-build-only`) test one function each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit, one `unit<n>.c` per case): 1302 base64 encode and decode, 1600 NTLM (also feature `NTLM`), 1601 MD5, 1610 SHA-256, 1611 MD4, 1612 HMAC, 1615 SHA-512/256 (feature `sha512-256`), 1979 AWS SigV4 `canon_string` and 1980 `canon_query`. Curl implements each by hand in its own library (find them with the Glossary and `rg`: cryptography, NTLM and authentication libraries; the SigV4 signer is in `Curl.Authentication.UnitLibrary`). Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program on the Curl type and returning its failure count. If the Curl type is `internal`, use the library's existing `InternalsVisibleTo` pattern or ask the ADR's rule; never make a type public only for the harness without saying so in Notes.

## Acceptance criteria

- [ ] Each of the nine cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match `tests/unit/unit<n>.c` of curl 8.22.0 row for row (the C file is quoted in the port's doc comment by name), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`.
- [ ] Each case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`; a case with no counterpart or one that still differs has its reason and first difference in Notes, is not listed, and the ADR from BL-2840 row is updated.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to this task's `touches` in the same commit.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

## Log

- 2026-10-19: Created.
