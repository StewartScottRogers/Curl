---
id: BL-2912
title: Port upstream unit programs unit1656, unit1657, unit1666 and unit1667 over Curl.Output's DER reader and text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Asn1.cs, Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Der, Curl.Conformance.UnitProgram.UnitTests/Asn1/Der, Curl.Output.UnitLibrary/Curl.Output.UnitLibrary.csproj]
requirement: none
created: 2026-10-21
completed:
---
# BL-2912 — Port upstream unit programs unit1656, unit1657, unit1666 and unit1667 over Curl.Output's DER reader and text

## Goal

Upstream cases 1656, 1657, 1666 and 1667 run as ported unit programs over `Curl.Output`'s DER reader and text and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2843. Follow ADR-0711 (shape) and ADR-0723: these four checks are compiled only into the Schannel build, so on Linux and macOS each port prints the C program's `not tested since <fn>() is not built in` line and passes. C sources: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1656.c (`GTime2str`), `unit1657.c` (`getASN1Element`, indefinite-length nesting 3/16 accepted, 17/1024 refused), `unit1666.c` (`encodeOID`), `unit1667.c` (`ASN1tostr` by tag). One `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Der/`, registered in `UnitPrograms.Asn1.cs`, tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Der/`.

Curl's counterparts are `internal` in `Curl.Output.UnitLibrary`: `DerReader.Read`/`TryRead` (getASN1Element, ADR-0054), `DerText.Format(der, element)` (ASN1tostr by tag), `DerText.FormatObjectIdentifier` and the private `EncodeObjectIdentifier` (encodeOID), and the time printer behind `DerText.Format` for GeneralizedTime (GTime2str). Add `<InternalsVisibleTo Include="Curl.Conformance.UnitProgram.UnitLibrary" />` to `Curl.Output.UnitLibrary.csproj` (one-line commit). If a port needs a private function (encodeOID's dotted form without the name lookup, GTime2str on raw text), make it `internal` in `DerText.cs` and add that file and `Curl.Output.UnitTests` to `touches` first. Where a port finds Curl wrong, fix it in `Curl.Output.UnitLibrary` with a focused test in `Curl.Output.UnitTests` (ADR-0482).

## Acceptance criteria

- [ ] `unit1656`, `unit1657`, `unit1666` and `unit1667` ports exist in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Der/`, each doc comment naming the C program whose rows it reproduces row for row, registered in `UnitPrograms.Asn1.cs`, each with tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Der/` (Windows and non-Windows answers pinned with `OSCondition`).
- [ ] With `$env:CURL_UPSTREAM_CASE` set to each of 1656, 1657, 1666 and 1667, `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"` passes on Windows, and the four numbers are on `PassingUpstreamCases.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` for `Curl.Conformance.UnitProgram.UnitLibrary` (and `Curl.Output.UnitLibrary` if changed) shows 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
