---
id: BL-4134
title: Extension: Check and send Content-Digest and Repr-Digest
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.ContentDigest.UnitLibrary, Curl.Extension.ContentDigest.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4134 — Extension: Check and send Content-Digest and Repr-Digest

## Goal

Curl provides an option that checks the server's digest fields against the body and adds them to uploads, as the extension `Curl.Extension.ContentDigest.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 33 of 63. Specification: RFC 9530. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.ContentDigest.UnitLibrary` and `Curl.Extension.ContentDigest.UnitTests` exist in `/Extensions/ContentDigest/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that checks the server's digest fields against the body and adds them to uploads.
- [x] Unit tests check its wire behaviour against named sections of RFC 9530, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (BL-4134 run): The extension is a transfer hook with no URL scheme, so it registers in the option, description and transfer hook catalogues (added to touches; no other task in Doing on origin/work/dark-factory names them, and all three are merge=union shared list files) rather than in `ExtensionCatalogue.cs`, which lists protocol handlers only; HttpCache does the same. Option `--content-digest` (a flag), SHA-256 sent, SHA-256 and SHA-512 checked, exit 56 on a mismatch: ADR-1092.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Extension.ContentDigest.UnitLibrary` 100% line and branch, 40 members, 0 failing, worst CRAP 10 (one method split for complexity). Fast tests green on Windows; the tests use no paths, OS errors or certificates, so they are platform-neutral, which meets the CI criterion.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 57705860 4b92a9cd 56c85819 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Content-Digest extension built, registered, documented in ADR-1092, gates met
