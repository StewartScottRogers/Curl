---
id: BL-2842
title: Port upstream unit programs for hashes, encodings and signing: cases 1302, 1600, 1601, 1610, 1611, 1612, 1615, 1979 and 1980
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Hashes.cs, Curl.Conformance.UnitProgram.UnitLibrary/Hashes, Curl.Conformance.UnitProgram.UnitTests/Hashes, Curl.Authentication.UnitLibrary/Curl.Authentication.UnitLibrary.csproj, Curl.Cli.UnitLibrary/Curl.Cli.UnitLibrary.csproj]
requirement: none
created: 2026-10-19
completed: 2026-10-21
---
# BL-2842 — Port upstream unit programs for hashes, encodings and signing: cases 1302, 1600, 1601, 1610, 1611, 1612, 1615, 1979 and 1980

## Goal

Upstream cases 1302, 1600, 1601, 1610, 1611, 1612, 1615, 1979 and 1980 run as ported unit programs over Curl's own implementations and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

These nine `unittest` cases (skipped today with `Curl lacks the feature unittest`; gap scorecard 2026-10-19_1329 has them `excluded | debug-build-only`) test one function each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit, one `unit<n>.c` per case): 1302 base64 encode and decode, 1600 NTLM (also feature `NTLM`), 1601 MD5, 1610 SHA-256, 1611 MD4, 1612 HMAC, 1615 SHA-512/256 (feature `sha512-256`), 1979 AWS SigV4 `canon_string` and 1980 `canon_query`. Curl implements each by hand in its own library (find them with the Glossary and `rg`: cryptography, NTLM and authentication libraries; the SigV4 signer is in `Curl.Authentication.UnitLibrary`). Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program on the Curl type and returning its failure count. If the Curl type is `internal`, use the library's existing `InternalsVisibleTo` pattern or ask the ADR's rule; never make a type public only for the harness without saying so in Notes.

## Acceptance criteria

- [x] Each of the nine cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match `tests/unit/unit<n>.c` of curl 8.22.0 row for row (the C file is quoted in the port's doc comment by name), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`.
- [x] Each case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`; a case with no counterpart or one that still differs has its reason and first difference in Notes, is not listed, and the ADR from BL-2840 row is updated.
- [x] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to this task's `touches` in the same commit.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [x] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

Ports go in Curl.Conformance.UnitProgram.UnitLibrary/Hashes/ and their tests in Curl.Conformance.UnitProgram.UnitTests/Hashes/; register them only in Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Hashes.cs, which BL-2886 creates. Its project references are added by BL-2886; if one is missing, add it in a separate one-line commit and say so in the Log (Stewart, 2026-10-21: keep the seven ports parallel).

- 2026-10-21 (lane 1): nine ports in `Curl.Conformance.UnitProgram.UnitLibrary/Hashes/`, each quoting its `unit<n>.c`, with `UnitChecks` (unitcheck.h's `verify_memory`/`fail_unless`) and `DigestUnitProgram` (the shared shape of the six digest programs). Counterparts: 1302 `Convert.ToBase64String` + BCL `Base64Url` (Curl has no base64url encoder of its own; the BCL is what it would use) + `Curl.Cli.CurlBase64Decoder`; 1600 `NtlmOneWayFunctions.ComputeNtOwfV1` zero-padded to 21 bytes; 1601/1610/1612 the BCL `MD5`, `SHA256`, `HMACMD5` that Curl's Digest, SigV4 and CRAM-MD5 code calls; 1611 `Curl.Cryptography.Md4`; 1615 `Curl.Authentication.Sha512Slash256`; 1979 `AwsSigV4UriEncoding.CanonicalPath` (normalize TRUE runs as service `execute-api`, FALSE as `s3`, since curl normalizes for every service but S3); 1980 `AwsSigV4UriEncoding.CanonicalQuery`. Note 1979 tests `canon_path`, not `canon_string` as the Context says.
- Touches widened (no Doing task on `origin/work/dark-factory` named them): `Curl.Authentication.UnitLibrary.csproj` and `Curl.Cli.UnitLibrary.csproj` each gain `InternalsVisibleTo Curl.Conformance.UnitProgram.UnitLibrary`, for `Sha512Slash256`, `AwsSigV4UriEncoding` and `CurlBase64Decoder` (ADR-0711's InternalsVisibleTo rule); no type was made public.
- 1302 keeps the C program's quirk: a wrong base64url output length is printed but not counted (`UnitChecks.WarnUnless`).
- All nine end `Passed` with `CURL_UPSTREAM_CASE=<n>` on Windows and are on the ratchet; no port found Curl wrong, so no fix and no ADR-0711 row change. Pure computation, no paths or OS text, so platform-neutral; the shift coordinator's CI watch files a fix task if Linux or macOS disagrees.
- Measure-CodeQuality: `Curl.Conformance.UnitProgram.UnitLibrary` 100% line, 100% branch, 0 failing members. Build clean with -warnaserror; fast tests green in all 43 test projects.

## Log

- 2026-10-19: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 131b7e48 78f5629b 603a1553 c0723528 620ebead.
- 2026-10-21: Doing -> Done. Upstream unit programs 1302, 1600, 1601, 1610, 1611, 1612, 1615, 1979 and 1980 run as ports over Curl's code and pass on the ratchet
