---
id: BL-4290
title: Extension: Azure Storage URL mapping and request authorization (SAS, Entra ID bearer, x-ms-version)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4288]
touches: [Curl.Extension.AzureStorage.UnitLibrary/Requests, Curl.Extension.AzureStorage.UnitTests/Requests]
requirement: none
created: 2026-10-29
completed:
---
# BL-4290 — Extension: Azure Storage URL mapping and request authorization (SAS, Entra ID bearer, x-ms-version)

## Goal

`Curl.Extension.AzureStorage.UnitLibrary` turns an `az://account/container/blob` or `abfs://filesystem@account.dfs.core.windows.net/path` URL into the endpoint, path and query to request, and authorizes it with a SAS token (appended to the query) or an Entra ID bearer token (`Authorization: Bearer`), always sending `x-ms-version` and `x-ms-date`.

## Context

Split from BL-4126 for size. Specification: Azure Blob Storage REST API ("Addressing Blob Service Resources", "Versioning for the Azure Storage services", "Authorize with Microsoft Entra ID", "Create a service SAS" / "Create an account SAS" for the token's form) and ADLS Gen2 REST API (the `abfs` URI scheme, the DFS endpoint). Decide and record in the ADR BL-4126 writes (leave a Notes line for it): the `x-ms-version` pinned, where the token comes from (an environment variable such as `AZURE_STORAGE_SAS_TOKEN` / `AZURE_STORAGE_BEARER_TOKEN`, or curl's `--oauth2-bearer`), and the host suffix for a bare account name. Shared Key signing may be left out with a reason. Code in `Requests/`; pure functions, unit tested with an injected `TimeProvider`.

## Acceptance criteria

- [ ] URL mapping for `az://` and `abfs://` and the SAS and bearer authorization exist under `Requests/`, with unit tests citing the named specification section for each.
- [ ] A URL with no account, or a SAS token that is not a query string, yields a typed failure.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
