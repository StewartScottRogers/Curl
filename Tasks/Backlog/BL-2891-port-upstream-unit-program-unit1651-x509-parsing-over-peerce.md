---
id: BL-2891
title: Port upstream unit program unit1651 (x509 parsing) over PeerCertificateText
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Asn1.cs, Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Certinfo, Curl.Conformance.UnitProgram.UnitTests/Asn1/Certinfo]
requirement: none
created: 2026-10-21
completed:
---
# BL-2891 — Port upstream unit program unit1651 (x509 parsing) over PeerCertificateText

## Goal

Upstream case 1651 runs as a ported unit program over `Curl.Output.PeerCertificateText` and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.

## Context

Split from BL-2843. Follow ADR-0711 (shape) and ADR-0720: unit1651 is compiled only into the GnuTLS and Schannel builds, so on Linux and macOS the port prints `not tested since Curl_extract_certinfo() is not built in` and passes. C source: https://github.com/curl/curl/blob/curl-8_22_0/tests/unit/unit1651.c - a 3,935-byte certificate captured from curl.se in 2018; `Curl_extract_certinfo` must succeed on it (one check), then the "poor man's fuzzing" overwrites each of the first 45 bytes with 1, 18, 35, ... 254 in turn and extracts again, results ignored. Port it over the public `Curl.Output.PeerCertificateText.Format(ReadOnlyMemory<byte>)`: the first call must not throw; in the fuzzing loop a `FormatException` (Curl's refusal) is ignored as the C program ignores the result, and any other exception is a failure. Keep the certificate bytes in their own file (e.g. `Unit1651Certificate.cs`). One `IUnitProgram` in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Certinfo/`, registered in `UnitPrograms.Asn1.cs`, tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Certinfo/`.

## Acceptance criteria

- [ ] A `unit1651` port exists in `Curl.Conformance.UnitProgram.UnitLibrary/Asn1/Certinfo/` whose doc comment names unit1651.c and whose checks match it, registered in `UnitPrograms.Asn1.cs`, with tests in `Curl.Conformance.UnitProgram.UnitTests/Asn1/Certinfo/` (Windows and non-Windows answers pinned with `OSCondition`).
- [ ] With `$env:CURL_UPSTREAM_CASE='1651'`, `dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"` passes on Windows, and 1651 is on `PassingUpstreamCases.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Conformance.UnitProgram.UnitLibrary` shows 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-21: Created.
