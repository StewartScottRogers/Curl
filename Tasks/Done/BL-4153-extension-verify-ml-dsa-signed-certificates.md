---
id: BL-4153
title: Extension: Verify ML-DSA signed certificates
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4097]
touches: [Curl.Extension.MlDsaCertificates.UnitLibrary, Curl.Extension.MlDsaCertificates.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionCertificateCheckHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4153 — Extension: Verify ML-DSA signed certificates

## Goal

Curl provides an option that accepts and verifies ML-DSA certificate signatures, as the extension `Curl.Extension.MlDsaCertificates.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 53 of 63. Specification: FIPS 204 and RFC 9881. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.MlDsaCertificates.UnitLibrary` and `Curl.Extension.MlDsaCertificates.UnitTests` exist in `/Extensions/MlDsaCertificates/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that accepts and verifies ML-DSA certificate signatures.
- [x] Unit tests check its wire behaviour against named sections of FIPS 204 and RFC 9881, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Option `--mldsa-verify` (no argument), a certificate check hook (ADR-1016), recorded in ADR-1121. It has no URL scheme, so like `--ratelimit-pace` (ADR-1106) it adds no line to `ExtensionCatalogue.cs`; it is registered in the option, description and certificate check hook catalogues instead (shared list files, added to touches for that).
- Default taken: the server's certificate must be ML-DSA signed, and every ML-DSA link must have its issuer in the chain sent (or be a last self-issued certificate), since the hook sees only the server's chain; anchors in `--cacert` are left out (ADR-1121).
- ML-DSA verification reuses `Curl.Cryptography.MlDsa.VerifyData` (a shared layer, as ObliviousHttp already references it); certificates are read with `System.Formats.Asn1`.
- `CURL_EXTENSIONS=0` and the listing are covered by `MlDsaCertificatesExtensionTests`: the option is absent from the command-line options, so curl's unknown-option exit 2 applies; without the option no hook activates, so every transfer is unchanged.
- Measure-CodeQuality.ps1 -Library Curl.Extension.MlDsaCertificates.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members.
- CI on Linux and macOS: the tests touch no file system, path, OS error or platform certificate store (certificates are built in memory), so they are platform-neutral; fast tests green here on Windows.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ffb8236c 8ba6b210 26425db2 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --mldsa-verify extension verifies ML-DSA certificate signatures (FIPS 204, RFC 9881); build clean, fast tests green, quality gates met
