---
id: BL-4292
title: OCI extension: oci:// pull of manifests and blobs
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4291]
touches: [Curl.Extension.OciRegistry.UnitLibrary/Client, Curl.Extension.OciRegistry.UnitTests/Client, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed:
---
# BL-4292 — OCI extension: oci:// pull of manifests and blobs

## Goal

`curl oci://registry/repo:tag` writes the manifest and `oci://registry/repo@sha256:...` a blob, through the registry token auth of BL-4291.

## Context

Split from BL-4127 (extension 26 of 63; ADR-0999). OCI Distribution Specification 1.1, "Pulling manifests" and "Pulling blobs": `GET /v2/<name>/manifests/<reference>` with an `Accept` of the OCI and Docker manifest and index media types, `GET /v2/<name>/blobs/<digest>` following a redirect, 401 answered by the Bearer token flow, error bodies (`errors[].code`) mapped to an exit code. Registers the extension in `ExtensionCatalogue.cs` (one line), adds its description to `curl --extensions` and `curl --ai-help extensions`, and writes the ADR.

## Acceptance criteria

- [ ] `OciRegistryExtension.CreateHandler` is registered by one line in `ExtensionCatalogue.cs` for the `oci` scheme.
- [ ] Manifest and blob pull work as above; unit tests with fake connections cite the specification's sections.
- [ ] With `CURL_EXTENSIONS=0` an `oci://` URL fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the scheme, URL form, specification version, and what is left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
