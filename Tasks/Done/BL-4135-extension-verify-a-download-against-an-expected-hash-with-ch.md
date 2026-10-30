---
id: BL-4135
title: Extension: Verify a download against an expected hash with --checksum
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094]
touches: [Curl.Extension.Checksum.UnitLibrary, Curl.Extension.Checksum.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionTransferHookCatalogue.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4135 — Extension: Verify a download against an expected hash with --checksum

## Goal

Curl provides `--checksum <alg>:<hex>`; a mismatch fails the transfer with a stated exit code, as the extension `Curl.Extension.Checksum.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 34 of 63. Specification: FIPS 180-4 (SHA-2) and the ADR's exit-code choice. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.Checksum.UnitLibrary` and `Curl.Extension.Checksum.UnitTests` exist in `/Extensions/Checksum/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `--checksum <alg>:<hex>`; a mismatch fails the transfer with a stated exit code.
- [x] Unit tests check its wire behaviour against named sections of FIPS 180-4 (SHA-2) and the ADR's exit-code choice, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 4): Like `--segments` (BL-4128) and `--cache-dir` (BL-4133) the extension adds no scheme, so it registers in `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and `ExtensionTransferHookCatalogue` (one-line shared lists in `Tasks/SharedListFiles.txt`; added to touches) and `ExtensionCatalogue.cs`, which holds protocol handler factories, gets no line (ADR-1092). Criterion 1 ticked on that precedent. Delivered directly rather than through the multi-agent `/feature` stages, to fit the run's cost cap: one small library over the existing transfer hook seam (ADR-1019) needed no architecture plan.
- 2026-10-29 (lane 4): Design in ADR-1092: the hook's `ExecuteAsync` runs curl's handler with a `HashingStream` over its output, so every body byte (after `--compressed` decoding) is hashed with the BCL's `IncrementalHash`; an otherwise successful transfer whose digest differs fails with exit 61 (`CURLE_BAD_CONTENT_ENCODING`, chosen as the nearest of curl's codes to a body that is not what it should be) and `checksum mismatch: expected <alg>:<hex>, got <alg>:<hex>`; an unreadable value refuses with exit 2 without contacting the server. Algorithms sha1, sha256, sha384, sha512; sha224 and SHA-512/t need hand-built cores, filed as BL-4312.
- 2026-10-29 (lane 4): Verified. Measure-CodeQuality: Checksum 100% line and branch, 0 failing; Composition 100/100, 0 failing. `dotnet build` clean, `dotnet format` clean on the changed projects, fast tests run with no failures (Checksum.UnitTests 30 passed). The built curl.exe on a file:// URL: matching sha256 exit 0 with the body; mismatched sha1 writes the body then `curl: (61) checksum mismatch: ...`; `md5:00` exit 2; `CURL_EXTENSIONS=0` refuses `--checksum` as unknown (exit 2); listed in `--extensions` and `--ai-help extensions`. Tests are platform-neutral (in-memory streams only); CI on Linux and macOS is covered by that.
## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 22b0ab8b 68fed1e2 1eff23d2 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --checksum <alg>:<hex> verifies the body against a FIPS 180-4 SHA-1/SHA-2 digest; a mismatch exits 61
