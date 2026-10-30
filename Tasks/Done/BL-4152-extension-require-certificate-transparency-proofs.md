---
id: BL-4152
title: Extension: Require Certificate Transparency proofs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4097]
touches: [Curl.Extension.CertificateTransparency.UnitLibrary, Curl.Extension.CertificateTransparency.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionCertificateCheckHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4152 — Extension: Require Certificate Transparency proofs

## Goal

Curl provides an option requiring valid SCTs on the server certificate, as the extension `Curl.Extension.CertificateTransparency.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 52 of 63. Specification: RFC 6962 and RFC 9162. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.CertificateTransparency.UnitLibrary` and `Curl.Extension.CertificateTransparency.UnitTests` exist in `/Extensions/CertificateTransparency/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option requiring valid SCTs on the server certificate.
- [x] Unit tests check its wire behaviour against named sections of RFC 6962 and RFC 9162, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.
- 2026-10-29 (lane 6): Done as `--require-sct <v3 JSON log list>` (ADR-1119), a certificate check hook (ADR-1016) that refuses a chain (exit 60) unless the leaf embeds at least one version 1 SCT from a listed log, no later than now, whose ECDSA or RSA SHA-256 signature verifies over the rebuilt precertificate entry (RFC 6962 §3.2, §3.3, §5.2; RFC 9162 §8.1.2, §8.1.3). TLS-extension and OCSP SCTs, v2 SCTs, inclusion proofs and browser CT policy are left out, with why, in the ADR.
- Registration: the extension has no URL scheme, so it adds no protocol handler line to `ExtensionCatalogue.cs` (which holds only handler factories, as for `ClientHints`); it is registered by one line each in `ExtensionOptionCatalogue.cs`, `ExtensionDescriptionCatalogue.cs` and `ExtensionCertificateCheckHookCatalogue.cs`, added to `touches` (all three are shared list files, so no other task is held).
- Checked: `curl --extensions` lists `certificate-transparency`; `CURL_EXTENSIONS=0 curl --require-sct x URL` prints `option --require-sct: is unknown` and exits 2. Measure-CodeQuality: CertificateTransparency and Composition 100% line and branch, 0 failing members. Full build clean, fast tests green (96 test assemblies passed). Tests are platform-neutral (in-memory certificates, a temp directory, no OS error text), which is how the CI criterion for Linux and macOS is met.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 23c7530f fa4ec049 b5ca63ae e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --require-sct refuses (exit 60) a server certificate without a valid embedded RFC 6962 SCT from a log in a v3 log list
