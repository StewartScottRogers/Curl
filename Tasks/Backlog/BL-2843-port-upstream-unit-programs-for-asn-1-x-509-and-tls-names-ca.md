---
id: BL-2843
title: Port upstream unit programs for ASN.1, X.509 and TLS names: cases 1394, 1397, 1651, 1656, 1657, 1666, 1667, 1676 and 3205
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2886]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Asn1.cs, Curl.Conformance.UnitProgram.UnitLibrary/Asn1, Curl.Conformance.UnitProgram.UnitTests/Asn1]
requirement: none
created: 2026-10-19
completed:
---
# BL-2843 — Port upstream unit programs for ASN.1, X.509 and TLS names: cases 1394, 1397, 1651, 1656, 1657, 1666, 1667, 1676 and 3205

## Goal

Upstream cases 1394, 1397, 1651, 1656, 1657, 1666, 1667, 1676 and 3205 run as ported unit programs over Curl's own ASN.1, X.509, certificate-name and cipher-name code and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or have a reason recorded.

## Context

These nine `unittest` cases (skipped today with `Curl lacks the feature unittest` or `the harness does not act on <client><tool>`; gap scorecard 2026-10-19_1329 has them `excluded | debug-build-only`) test one function each in upstream curl 8.22.0 (https://github.com/curl/curl/tree/curl-8_22_0/tests/unit and `/tests/tunit`): 1394 `parse_cert_parameter()` (tool program `tool1394`), 1397 `Curl_cert_hostcheck` (certificate host name matching), 1651 x509 parsing, 1656 `GTime2str`, 1657 `getASN1Element`, 1666 `encodeOID`, 1667 `ASN1tostr`, 1676 x509 DH public key parameter extraction, 3205 cipher suite name lookup. Curl does the ASN.1 and certificate work by hand or with `System.Security.Cryptography.X509Certificates`; find the type for each with the Glossary and `rg` (the TLS libraries and `Curl.Cli.UnitLibrary` for `--cert` parameter parsing). Follow the ADR from BL-2840 and the mechanism from BL-2841: one `IUnitProgram` per case in `Curl.Conformance.UnitProgram.UnitLibrary`, registered in `UnitPrograms.Ported`, reproducing every check of the C program and returning its failure count. A case whose function has no Curl counterpart (an OpenSSL-internal helper, say) is recorded with that reason, never "hard".

## Acceptance criteria

- [ ] Each of the nine cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` whose checks match the C program's row for row (named in its doc comment), registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`; or the case has a Notes entry naming the C-only thing it tests and the ADR from BL-2840 row says so.
- [ ] Each ported case ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [ ] Where a port finds Curl wrong, the fix has a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482) and the library folder is added to `touches` in the same commit.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.
- [ ] After the push, `CI` is green on Linux and macOS for the commit.

## Notes

Ports go in Curl.Conformance.UnitProgram.UnitLibrary/Asn1/ and their tests in Curl.Conformance.UnitProgram.UnitTests/Asn1/; register them only in Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.Asn1.cs, which BL-2886 creates. Its project references are added by BL-2886; if one is missing, add it in a separate one-line commit and say so in the Log (Stewart, 2026-10-21: keep the seven ports parallel).

## Log

- 2026-10-19: Created.
