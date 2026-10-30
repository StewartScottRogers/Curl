---
id: BL-4127
title: Extension: Pull and push OCI artifacts with oci://
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4291, BL-4292, BL-4293]
touches: [Curl.Extension.OciRegistry.UnitLibrary, Curl.Extension.OciRegistry.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4127 — Extension: Pull and push OCI artifacts with oci://

## Goal

Curl provides `oci://registry/repo:tag` URLs: manifest and blob pull/push with registry token auth, as the extension `Curl.Extension.OciRegistry.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 26 of 63. Specification: OCI Distribution Specification 1.1. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.OciRegistry.UnitLibrary` and `Curl.Extension.OciRegistry.UnitTests` exist in `/Extensions/OciRegistry/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `oci://registry/repo:tag` URLs: manifest and blob pull/push with registry token auth.
- [x] Unit tests check its wire behaviour against named sections of OCI Distribution Specification 1.1, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 7): Split for size before any code: an HTTP/1.1 exchange plus Bearer token auth (BL-4291), pull (BL-4292) and push (BL-4293). Extensions cannot share the S3, OAuth2 or McpClient HTTP code, so it is written again here. Once all three are Done, this task checks its criteria and runs the quality gates over the whole library.
- 2026-10-29 (lane 1): BL-4291, BL-4292 and BL-4293 delivered the library; this run checked every criterion. ADR-1093 (pull: scheme `oci`, OCI Distribution Specification 1.1.0, left out) and ADR-1097 (push) together are the ADR. The description in `--extensions` and `--ai-help extensions` named only pulls, so it now says what `-T` pushes too. `CURL_EXTENSIONS=0 curl oci://...` measured: exit 1, `curl: (1) Protocol "oci" not supported`, pinned by OciRegistryExtensionTests. Measure-CodeQuality.ps1 -Library Curl.Extension.OciRegistry.UnitLibrary: 0 failing members. Fast tests: 0 failed (OciRegistry 155 passed). Linux and macOS: the change is a description string, platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8f5bbfd4 0c7ef334 ad0964b3 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size into BL-4291, BL-4292 and BL-4293; waits on them
- 2026-10-29: Backlog -> Doing. Criteria at claim: 8f5bbfd4 0c7ef334 ad0964b3 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. oci:// pulls and pushes manifests and blobs with registry Bearer tokens, listed in --extensions and --ai-help extensions, at the quality gates
