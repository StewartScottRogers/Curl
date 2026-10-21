---
id: BL-2889
title: Port upstream unit programs tool1394, unit1397, unit1676 and unit3205 and fix the empty-label wildcard match
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Asn1.cs, Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Names, Curl.Conformance.UnitProgram.UnitTests/Asn1/Names, Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs, Curl.Networking.UnitTests/SchannelCommonNameCheckTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-2889 — Port upstream unit programs tool1394, unit1397, unit1676 and unit3205 and fix the empty-label wildcard match

## Goal

Upstream cases 1394, 1397, 1676 and 3205 run as ported unit programs and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2843. Follow ADR-0711 (shape) and ADR-0720 (which guarded branch runs on which platform). C sources: https://github.com/curl/curl/blob/curl-8_22_0/tests/tunit/tool1394.c and `tests/unit/unit1397.c`, `unit1676.c`, `unit3205.c`. One `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Names/`, registered in `UnitPrograms.Asn1.cs`, tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Names/`.

- `tool1394` over the public `Curl.Networking.ClientCertificateArgument.Split(value, recognisesDriveLetters: OperatingSystem.IsWindows())`; the `#ifdef _WIN32` rows run on Windows only. `parse_cert_parameter` returning NULL passphrase is `Split`'s `null`.
- `unit1397` over the public `Curl.Networking.SchannelCommonNameCheck.PatternMatchesHost(pattern, host)` (the class is `internal static` but the method is public: the class needs to be reachable, so either add `<InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary" />` to `Curl.Networking.UnitLibrary.csproj` in a one-line commit, adding that csproj to `touches`, or reach it another way). Bytes `\xff`, `\xfe` are Latin-1 chars. Known Curl bug, found by reading: host `.hello.com` against `*.hello.com` must not match (upstream: the wildcard must match at least one character, `hostname_label_end - hostname >= pattern_label_end - pattern`), but `WildcardMatchesHost` accepts an empty left-most host label. Fix it with a focused test in `SchannelCommonNameCheckTests.cs` (ADR-0482).
- `unit1676` over the public `Curl.Output.PeerCertificateText.Format(cert)`: find the `dh(p):`, `dh(g):`, `dh(pub_key):` records (17, 34, 51). On Linux/macOS it only prints `not tested since Curl_extract_certinfo() is not built in` (ADR-0720).
- `unit3205` compiles to nothing on the Schannel and OpenSSL builds: the port does nothing and returns 0 (ADR-0720).

## Acceptance criteria

- [ ] `tool1394`, `unit1397`, `unit1676` and `unit3205` ports exist in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Names/`, each doc comment naming the C program its rows come from, registered in `UnitPrograms.Asn1.cs`, each with tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Names/` (platform-specific answers pinned with `OSCondition`).
- [ ] With `$env:CURL_UPSTREAM_CASE` set to each of 1394, 1397, 1676 and 3205, `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"` passes on Windows, and the four numbers are on `PassingUpstreamCases.txt`.
- [ ] `PatternMatchesHost("*.hello.com", ".hello.com")` is false, pinned by a test in `Curl.Networking.UnitTests/SchannelCommonNameCheckTests.cs`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitProgram.UnitLibrary,Curl.Networking.UnitLibrary` shows 100% line and branch coverage and complexity at most 10 for both.

## Notes

## Log

- 2026-10-21: Created.
