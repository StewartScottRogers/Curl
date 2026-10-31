---
id: BL-4290
title: Extension: Azure Storage URL mapping and request authorization (SAS, Entra ID bearer, x-ms-version)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4288]
touches: [Curl.Extension.AzureStorage.UnitLibrary/Requests, Curl.Extension.AzureStorage.UnitTests/Requests, Curl.Extension.AzureStorage.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4290 — Extension: Azure Storage URL mapping and request authorization (SAS, Entra ID bearer, x-ms-version)

## Goal

`Curl.Extension.AzureStorage.UnitLibrary` turns an `az://account/container/blob` or `abfs://filesystem@account.dfs.core.windows.net/path` URL into the endpoint, path and query to request, and authorizes it with a SAS token (appended to the query) or an Entra ID bearer token (`Authorization: Bearer`), always sending `x-ms-version` and `x-ms-date`.

## Context

Split from BL-4126 for size. Specification: Azure Blob Storage REST API ("Addressing Blob Service Resources", "Versioning for the Azure Storage services", "Authorize with Microsoft Entra ID", "Create a service SAS" / "Create an account SAS" for the token's form) and ADLS Gen2 REST API (the `abfs` URI scheme, the DFS endpoint). Decide and record in the ADR BL-4126 writes (leave a Notes line for it): the `x-ms-version` pinned, where the token comes from (an environment variable such as `AZURE_STORAGE_SAS_TOKEN` / `AZURE_STORAGE_BEARER_TOKEN`, or curl's `--oauth2-bearer`), and the host suffix for a bare account name. Shared Key signing may be left out with a reason. Code in `Requests/`; pure functions, unit tested with an injected `TimeProvider`.

## Acceptance criteria

- [x] URL mapping for `az://` and `abfs://` and the SAS and bearer authorization exist under `Requests/`, with unit tests citing the named specification section for each.
- [x] A URL with no account, or a SAS token that is not a query string, yields a typed failure.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added the library's CLAUDE.md to touches: it describes each folder, so the new `Requests/` needs its line; no other Doing task names it.
- For BL-4126's ADR (decided by Claude under Stewart's delegation; record there): `x-ms-version` is pinned to `2025-01-05`, a published version later than 2017-11-09 (the least that accepts bearer tokens). The credential is `--oauth2-bearer` first (curl's own bearer option), then `AZURE_STORAGE_BEARER_TOKEN`, then `AZURE_STORAGE_SAS_TOKEN`, else anonymous; an empty value counts as unset. A bare account name (no dot) gets `.blob.core.windows.net` for `az` and `.dfs.core.windows.net` for `abfs`/`abfss`; a host with a dot is used as it is, so other clouds' endpoints work. `abfs` maps `filesystem@host/path` to `/filesystem/path`.
- Shared Key signing is left out: it needs the account key in the client, which Microsoft advises against in favour of SAS or Entra ID, and those two cover the use cases. A SAS token must be `name=value` pairs joined by `&` with a `sig`, with no space, `#` or non-ASCII; a bearer token must be visible ASCII (RFC 6750 §2.1), which also stops header injection.
- Measure-CodeQuality.ps1 for Curl.Extension.AzureStorage.UnitLibrary: 100% line, 100% branch, 0 failing members, worst CRAP 10 (SasQuery was split once to bring its complexity from 12 under 10). Build clean, fast tests green on Windows; the tests use no paths or OS text, so they are platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: aaa6300d caaae3c8 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. az and abfs URLs map to Blob and DFS endpoints; SAS and Entra ID bearer authorization with x-ms-version and x-ms-date; quality gates met.
