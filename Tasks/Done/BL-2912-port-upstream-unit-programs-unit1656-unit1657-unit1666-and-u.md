---
id: BL-2912
title: Port upstream unit programs unit1656, unit1657, unit1666 and unit1667 over Curl.Output's DER reader and text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Asn1.cs, Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Der, Curl.Conformance.UnitProgram.UnitTests/Asn1/Der, Curl.Output.UnitLibrary/Curl.Output.UnitLibrary.csproj, Curl.Output.UnitLibrary/DerText.cs, Curl.Output.UnitLibrary/DerTextException.cs, Curl.Output.UnitTests/DerTextTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2912 — Port upstream unit programs unit1656, unit1657, unit1666 and unit1667 over Curl.Output's DER reader and text

## Goal

Upstream cases 1656, 1657, 1666 and 1667 run as ported unit programs over `Curl.Output`'s DER reader and text and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2843. Follow ADR-0711 (shape) and ADR-0723: these four checks are compiled only into the Schannel build, so on Linux and macOS each port prints the C program's `not tested since <fn>() is not built in` line and passes. C sources: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1656.c (`GTime2str`), `unit1657.c` (`getASN1Element`, indefinite-length nesting 3/16 accepted, 17/1024 refused), `unit1666.c` (`encodeOID`), `unit1667.c` (`ASN1tostr` by tag). One `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Der/`, registered in `UnitPrograms.Asn1.cs`, tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Der/`.

Curl's counterparts are `internal` in `Curl.Output.UnitLibrary`: `DerReader.Read`/`TryRead` (getASN1Element, ADR-0054), `DerText.Format(der, element)` (ASN1tostr by tag), `DerText.FormatObjectIdentifier` and the private `EncodeObjectIdentifier` (encodeOID), and the time printer behind `DerText.Format` for GeneralizedTime (GTime2str). Add `<InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary" />` to `Curl.Output.UnitLibrary.csproj` (one-line commit). If a port needs a private function (encodeOID's dotted form without the name lookup, GTime2str on raw text), make it `internal` in `DerText.cs` and add that file and `Curl.Output.UnitTests` to `touches` first. Where a port finds Curl wrong, fix it in `Curl.Output.UnitLibrary` with a focused test in `Curl.Output.UnitTests` (ADR-0482).

## Acceptance criteria

- [x] `unit1656`, `unit1657`, `unit1666` and `unit1667` ports exist in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Der/`, each doc comment naming the C program whose rows it reproduces row for row, registered in `UnitPrograms.Asn1.cs`, each with tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Der/` (Windows and non-Windows answers pinned with `OSCondition`).
- [x] With `$env:CURL_UPSTREAM_CASE` set to each of 1656, 1657, 1666 and 1667, `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"` passes on Windows, and the four numbers are on `PassingUpstreamCases.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for `Curl.Conformance.UnitProgram.UnitLibrary` (and `Curl.Output.UnitLibrary` if changed) shows 100% line and branch coverage and complexity at most 10.

## Notes

- Touches widened to `Curl.Output.UnitLibrary/DerText.cs`, a new `DerTextException.cs` and `Curl.Output.UnitTests/DerTextTests.cs`, as the Context allows: no task in Doing on `origin/work/dark-factory` named them. Why: `encodeOID` was private (now `internal DerText.EncodeObjectIdentifier`, which refuses empty content as curl's does), and unit1667 expects `CURLE_WEIRD_SERVER_REPLY` for a UniversalString code point past 0x1FFFFF where every other refusal is `CURLE_BAD_FUNCTION_ARGUMENT`, so `DerText` now throws `DerTextException` (a `FormatException`, so `PeerCertificateText` is unchanged) carrying that `CurlCode`.
- Choice: the C programs' small `dynbuf` limits (100 bytes for unit1666, 40 for unit1667, 32 KiB for unit1656) belong to the test programs, not to curl's printers, so the ports apply them (`Asn1/Der/DerTextOutcome.Print`): text that with its NUL passes the limit is `CURLE_TOO_LARGE`. unit1667's content is each C literal's first `size` bytes with its NUL, and its text is compared as a C string (up to the first NUL), as `strlen`/`strcmp` do.
- Each port has a `(bool)` constructor over Curl's real code so the row checks run on every platform in tests; the parameterless one runs them on Windows only (ADR-0723). No Curl defect found: every row of the four programs passed against Curl's existing DER code on the first run.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitProgram.UnitLibrary,Curl.Output.UnitLibrary`: both 100% line and branch, 0 failing members, worst CRAP 10. Fast tests green across the solution; the four cases pass through the case runner with `CURL_UPSTREAM_CASE` on Windows.


## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 18dcba38 b0b30ff9 da19ee8c.
- 2026-10-21: Doing -> Done. unit1656, unit1657, unit1666 and unit1667 run as ports over Curl.Output's DER reader and text and are on the ratchet
