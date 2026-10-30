---
id: BL-4138
title: Extension: Redeem Privacy Pass tokens
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.PrivacyPass.UnitLibrary, Curl.Extension.PrivacyPass.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4138 — Extension: Redeem Privacy Pass tokens

## Goal

Curl provides an option that answers a `PrivateToken` challenge with a token from an issuer, as the extension `Curl.Extension.PrivacyPass.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 37 of 63. Specification: RFC 9576, RFC 9577, RFC 9578. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.PrivacyPass.UnitLibrary` and `Curl.Extension.PrivacyPass.UnitTests` exist in `/Extensions/PrivacyPass/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that answers a `PrivateToken` challenge with a token from an issuer.
- [x] Unit tests check its wire behaviour against named sections of RFC 9576, RFC 9577, RFC 9578, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Plan (ADR-1095): `--privacy-pass <issuer request URL>` is a transfer hook like OAuth2. A 401 with a `PrivateToken` challenge of type 2 is answered by RFC 9578 §6 Blind RSA issuance (RFC 9474 blinding, hand-written on BigInteger) and the transfer is sent again with `Authorization: PrivateToken`. The HTTP client is copied from the OCI extension, since extensions never reference each other.
- Registration: the extension adds an option and no scheme handler, so its one line each goes in ExtensionOptionCatalogue, ExtensionDescriptionCatalogue and ExtensionTransferHookCatalogue (shared list files, added to touches), not in ExtensionCatalogue (which holds only handler factories), as with OAuth2. The build (VerifyExtensionIsolation) is clean.
- Checked: `curl --extensions` and `--ai-help extensions` list privacy-pass; `CURL_EXTENSIONS=0 curl --privacy-pass ...` exits 2. The quality report has 100% line and branch coverage and no member failing. The solution build is clean and the fast tests are green. The tests are platform-neutral (no paths, OS text or certificates), so they meet the Linux and macOS criterion.
- Filed BL-4315 (token type 1, VOPRF P-384) and BL-4316 (issuer directory discovery).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 535aa034 3ea8d9b3 56fda30b e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --privacy-pass answers a 401 PrivateToken challenge with a Blind RSA token from the issuer
