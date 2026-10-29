---
id: BL-4126
title: Extension: Read and write Azure Blob and Data Lake storage with az://
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4288, BL-4289, BL-4290]
touches: [Curl.Extension.AzureStorage.UnitLibrary, Curl.Extension.AzureStorage.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4126 — Extension: Read and write Azure Blob and Data Lake storage with az://

## Goal

Curl provides `az://` and `abfs://` URLs with SAS and Entra ID bearer tokens, as the extension `Curl.Extension.AzureStorage.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 25 of 63. Specification: Azure Blob Storage REST API, ADLS Gen2 REST API. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [ ] `Curl.Extension.AzureStorage.UnitLibrary` and `Curl.Extension.AzureStorage.UnitTests` exist in `/Extensions/AzureStorage/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [ ] It provides `az://` and `abfs://` URLs with SAS and Entra ID bearer tokens.
- [ ] Unit tests check its wire behaviour against named sections of Azure Blob Storage REST API, ADLS Gen2 REST API, with no network (fake connections or recorded bytes).
- [ ] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [ ] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 5): Split for size before any code, following the S3 split (BL-4197, BL-4198, BL-4199). Filed BL-4288 (project scaffold and HTTP/1.1 exchange over IConnection, written here because an extension may not reference Curl.Protocol.Http or another extension), BL-4289 (List Blobs, Block List, Error XML and ADLS Gen2 Path List JSON bodies) and BL-4290 (az:// and abfs:// URL mapping, SAS and Entra ID bearer authorization, x-ms-version). This task keeps the handler (GET, -T PUT with Put Block above a block size, listing for an empty path or trailing slash), registration, description, ADR and the CURL_EXTENSIONS=0 test. Added ExtensionDescriptionCatalogue.cs to touches for the `--extensions` description (a union-merged shared list file in Tasks/SharedListFiles.txt).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ca1336b9 cd2f5c62 96b0df1b bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4288 (project and HTTP/1.1 exchange), BL-4289 (XML and JSON bodies) and BL-4290 (URL mapping and authorization)
