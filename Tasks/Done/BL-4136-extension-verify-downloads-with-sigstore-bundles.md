---
id: BL-4136
title: Extension: Verify downloads with Sigstore bundles
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.Sigstore.UnitLibrary, Curl.Extension.Sigstore.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4136 — Extension: Verify downloads with Sigstore bundles

## Goal

Curl provides an option taking a Sigstore bundle and expected identity, as the extension `Curl.Extension.Sigstore.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 35 of 63. Specification: Sigstore bundle format and cosign verification. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Sigstore.UnitLibrary` and `Curl.Extension.Sigstore.UnitTests` exist in `/Extensions/Sigstore/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option taking a Sigstore bundle and expected identity.
- [x] Unit tests check its wire behaviour against named sections of Sigstore bundle format and cosign verification, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Plan: a transfer hook, as `--zsync` (ADR-1092): `--sigstore-bundle`, `--sigstore-identity`, `--sigstore-issuer`, `--sigstore-root`; the body is held back, verified as `cosign verify-blob` does (digest, Fulcio chain at `integratedTime` for code signing, ECDSA P-256 signature, SAN identity, issuer extension) and written only when it passes, else exit 60. Decisions and what is left out (Rekor proofs, DSSE, public keys, regexp identities, RFC 3161) in ADR-1101.
- Touches widened by the three catalogue files a transfer hook extension registers in (option, description, transfer hook); all three are in `Tasks/SharedListFiles.txt` (one entry per line, merge=union). BL-4125 in Doing names ExtensionDescriptionCatalogue.cs too, but a shared list file takes lines from several lanes, so it does not block. It is a transfer hook, not a protocol handler, so its one registration line is in `ExtensionTransferHookCatalogue.cs` (as `--zsync`), and `ExtensionCatalogue.cs` is unchanged.
- Default: the trust root is a user-supplied PEM file rather than an embedded Sigstore public-good root, which would go stale without a TUF client (ADR-1101).
- CURL_EXTENSIONS=0: `SigstoreExtensionTests.Composition_RegistersTheOptionsOnlyWhileExtensionsAreOn` shows the options leave the option table, so they are refused as unknown (exit 2) as for every extension; without `--sigstore-bundle` no hook is activated, so transfers are unchanged.
- Measure-CodeQuality.ps1 -Library Curl.Extension.Sigstore.UnitLibrary: 100% line, 100% branch, 0 failing members (after splitting three methods past complexity 10). 38 tests.
- Full fast run: all green except Curl.Extension.Coap.UnitTests LostServerFinishedFlight, which passed alone; filed as BL-4313.
- Linux and macOS: tests use only temp-directory files and certificates generated in memory (CertificateRequest), no paths or OS error text pinned, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 1c5de1e8 0e94d25a 48fdde70 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --sigstore-bundle verifies a download against a Sigstore bundle, Fulcio root and signer identity before writing it
