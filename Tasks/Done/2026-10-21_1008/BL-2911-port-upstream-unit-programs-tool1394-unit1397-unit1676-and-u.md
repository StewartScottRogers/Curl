---
id: BL-2911
title: Port upstream unit programs tool1394, unit1397, unit1676 and unit3205 and fix the empty-label wildcard match
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Asn1.cs, Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Names, Curl.Conformance.UnitProgram.UnitTests/Asn1/Names, Curl.Networking.UnitLibrary/SchannelCommonNameCheck.cs, Curl.Networking.UnitTests/SchannelCommonNameCheckTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2911 — Port upstream unit programs tool1394, unit1397, unit1676 and unit3205 and fix the empty-label wildcard match

## Goal

Upstream cases 1394, 1397, 1676 and 3205 run as ported unit programs and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2843. Follow ADR-0711 (shape) and ADR-0723 (which guarded branch runs on which platform). C sources: https://github.com/curl/curl/blob/curl-8_22_0/tests/tunit/tool1394.c and `tests/unit/unit1397.c`, `unit1676.c`, `unit3205.c`. One `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Names/`, registered in `UnitPrograms.Asn1.cs`, tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Names/`.

- `tool1394` over the public `Curl.Networking.ClientCertificateArgument.Split(value, recognisesDriveLetters: OperatingSystem.IsWindows())`; the `#ifdef _WIN32` rows run on Windows only. `parse_cert_parameter` returning NULL passphrase is `Split`'s `null`.
- `unit1397` over the public `Curl.Networking.SchannelCommonNameCheck.PatternMatchesHost(pattern, host)` (the class is `internal static` but the method is public: the class needs to be reachable, so either add `<InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary" />` to `Curl.Networking.UnitLibrary.csproj` in a one-line commit, adding that csproj to `touches`, or reach it another way). Bytes `\xff`, `\xfe` are Latin-1 chars. Known Curl bug, found by reading: host `.hello.com` against `*.hello.com` must not match (upstream: the wildcard must match at least one character, `hostname_label_end - hostname >= pattern_label_end - pattern`), but `WildcardMatchesHost` accepts an empty left-most host label. Fix it with a focused test in `SchannelCommonNameCheckTests.cs` (ADR-0482).
- `unit1676` over the public `Curl.Output.PeerCertificateText.Format(cert)`: find the `dh(p):`, `dh(g):`, `dh(pub_key):` records (17, 34, 51). On Linux/macOS it only prints `not tested since Curl_extract_certinfo() is not built in` (ADR-0723).
- `unit3205` compiles to nothing on the Schannel and OpenSSL builds: the port does nothing and returns 0 (ADR-0723).

## Acceptance criteria

- [x] `tool1394`, `unit1397`, `unit1676` and `unit3205` ports exist in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Names/`, each doc comment naming the C program its rows come from, registered in `UnitPrograms.Asn1.cs`, each with tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Names/` (platform-specific answers pinned with `OSCondition`).
- [x] With `$env:CURL_UPSTREAM_CASE` set to each of 1394, 1397, 1676 and 3205, `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"` passes on Windows, and the four numbers are on `PassingUpstreamCases.txt`.
- [x] `PatternMatchesHost("*.hello.com", ".hello.com")` is false, pinned by a test in `Curl.Networking.UnitTests/SchannelCommonNameCheckTests.cs`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitProgram.UnitLibrary,Curl.Networking.UnitLibrary` shows 100% line and branch coverage and complexity at most 10 for both.

## Notes

- Reached `PatternMatchesHost` by making `SchannelCommonNameCheck` public (in `touches`) rather than adding `InternalsVisibleTo` to `Curl.Networking.UnitLibrary.csproj`, which BL-2894 in Doing touches.
- Fix: `WildcardMatchesHost` now needs a host label of at least one character (`hostLabelEnd < 1`), as upstream's `hostname_label_end - hostname >= pattern_label_end - pattern`; pinned by `PatternMatchesHost_WildcardAgainstAnEmptyLeftMostLabel_IsFalse`. Every other unit1397 row already matched.
- Each port takes its Curl function and platform choice through its constructor (default: the real function and `OperatingSystem.IsWindows()`), so both ADR-0723 branches are covered on Windows; `OSCondition` tests pin the registered port's branch per platform. Failure lines go through `Hashes.UnitChecks` to standard error; unit1676's OpenSSL-build line goes to standard output, as `puts` does.
- `PeerCertificateText.Format` already gives `dh(p):17`, `dh(g):34`, `dh(pub_key):51` for unit1676's certificate; no Curl.Output change.
- Measured: 4 manual upstream cases pass on Windows; Measure-CodeQuality: Curl.Conformance.UnitProgram.UnitLibrary 100/100, max complexity 8; Curl.Networking.UnitLibrary 100/100, max 10; 0 failing members.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 7de64f9c a717d77f 6ab2d507 c4fd7eb2.
- 2026-10-21: Doing -> Done. Upstream cases 1394, 1397, 1676 and 3205 run as ported unit programs and are on the ratchet; *.hello.com no longer matches .hello.com
