---
id: BL-4152
title: Extension: Require Certificate Transparency proofs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4097]
touches: [Curl.Extension.CertificateTransparency.UnitLibrary, Curl.Extension.CertificateTransparency.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4152 — Extension: Require Certificate Transparency proofs

## Goal

Curl provides an option requiring valid SCTs on the server certificate, as the extension `Curl.Extension.CertificateTransparency.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 52 of 63. Specification: RFC 6962 and RFC 9162. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.CertificateTransparency.UnitLibrary` and `Curl.Extension.CertificateTransparency.UnitTests` exist in `/Extensions/CertificateTransparency/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides an option requiring valid SCTs on the server certificate.
- [ ] Unit tests check its wire behaviour against named sections of RFC 6962 and RFC 9162, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 23c7530f fa4ec049 b5ca63ae e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
