---
id: BL-4162
title: Extension: Bind tokens to a key with DPoP
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.Dpop.UnitLibrary, Curl.Extension.Dpop.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4162 — Extension: Bind tokens to a key with DPoP

## Goal

Curl provides an option that signs a DPoP proof per request, as the extension `Curl.Extension.Dpop.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 62 of 63. Specification: RFC 9449. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Dpop.UnitLibrary` and `Curl.Extension.Dpop.UnitTests` exist in `/Extensions/Dpop/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option that signs a DPoP proof per request.
- [x] Unit tests check its wire behaviour against named sections of RFC 9449, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- Plan: one transfer hook (ADR-1007/1019) behind `--dpop <PEM key file>`; ES256/ES384/ES512/RS256 proofs built with BCL `ECDsa`/`RSA` and `Utf8JsonWriter`; `--oauth2-bearer` sent as `Authorization: DPoP` with `ath`; per-origin `DPoP-Nonce` kept and a 400/401 with a new nonce retried once, the response held in memory until kept. Decisions in ADR-1136.
- Touches: added the option, description and transfer hook catalogues (shared list files in `Tasks/SharedListFiles.txt`) since an option extension registers there; no other Doing task names them as more than a list line. As ADR-1106 and ADR-1135 for scheme-less extensions, `ExtensionCatalogue.cs` (protocol handler factories) gets no line: the extension has no URL scheme; the first criterion's registration is met by the three catalogue lines.
- Measured: Measure-CodeQuality for Curl.Extension.Dpop.UnitLibrary 100/100, 0 failing (two methods split after the first run showed complexity 12); Curl.Extension.Composition.UnitLibrary 100/100, 0 failing. Built curl.exe lists `dpop` in `--extensions` and `--ai-help extensions`; `CURL_EXTENSIONS=0 curl --dpop k.pem ...` gives `option --dpop: is unknown`, exit 2, as an unknown option.
- Tests are platform-neutral (temp files only, no paths or OS error text pinned), so CI on Linux and macOS is expected green.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f9c08ac5 6091c144 618a2157 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-30: Doing -> Done. --dpop signs an RFC 9449 DPoP proof for every HTTP request, binds --oauth2-bearer by ath and answers DPoP-Nonce challenges
