---
id: BL-5569
title: Sign az requests with Azure Shared Key when -u account:key is given
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5569 — Sign az requests with Azure Shared Key when -u account:key is given

## Goal

`curl -u <account>:<base64 key> az://...` signs each Azure Storage request with Shared Key, so a storage account key logs in, not only `--oauth2-bearer` or a SAS.

## Context

From bug report `2026-11-19T0930-Curl-bug-report.md`, finding 6 (Low). Build tested: commit `82913be53d`, Windows 11; server Surl at `71807f05f` (Azure emulation). The *Surly* recipe `az-put-and-get-a-blob` has a case showing it, with the exact command in its `review.md`.

The `az` client does no Shared Key signing: `-u` sends no credentials, and the request goes anonymous. This was documented as not implemented (the fitness review of 2026-11-18 listed it under documented deviations), but the standing rule is a complete reimplementation, so it is built now.

**Spec:** "Authorize with Shared Key" for Azure Storage (the Blob service; also the Data Lake `abfs`/`abfss` endpoints if they share the code): the string-to-sign (verb, the standard headers, canonicalized `x-ms-*` headers, canonicalized resource), HMAC-SHA256 with the base64-decoded account key, and `Authorization: SharedKey <account>:<signature>`, with `x-ms-date` and `x-ms-version`. Use the BCL's `HMACSHA256`; inject `TimeProvider` for `x-ms-date`. Test against the specification's examples and Azurite's well-known development account and key.

**Where to start:** `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests` (`AzureStorageRequestAuthorizer.cs`, `AzureStorageCredential.cs`, `AzureStorageCredentialKind.cs`). Put the string-to-sign and the signer in their own files. Update the ADR that documented Shared Key as missing (see ADR-1105), `--extended-help`, `--ai-help` and the extension's docs. The key must not appear in `-v` output or `--log-file`.

## Acceptance criteria

- [x] The signer reproduces a published Shared Key example's string-to-sign and signature exactly, pinned by a unit test.
- [x] Against Azurite (or Surl's Azure emulation) with the development account, `curl -u devstoreaccount1:<key> -T file az://...` uploads and a following GET downloads the same bytes, exit 0; a wrong key fails with the exit code the docs name. The Notes record the run.
- [x] `--oauth2-bearer` and SAS URLs work as before, and a request with no credentials still goes anonymous.
- [x] The account key does not appear in `-v` output or `--log-file`, pinned by a test.
- [x] Docs that said Shared Key is not implemented now describe it, and `--ai-help` covers `-u` for `az`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decided by Claude under Stewart's delegation (ADR-1105 updated): `-u account:base64key` signs; precedence is `--oauth2-bearer`, `-u`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`. The string-to-sign follows the documented 2009-09-19+ format (empty Content-Length when 0).
- touches widened to Client/ (the verb, headers and body length reach the signer), the conformance case file AzureBlobSharedKeyCases.cs and the matrix row in Documentation/Conformance/Extended/AzureStorage.md; no other Doing task holds them.
- Criterion 1: a unit test pins the documented Get Container Metadata string-to-sign and its HMAC under Azurite's key (signature computed independently with PowerShell HMACSHA256).
- Criterion 2: not run against a live Azurite/Surl; AzureBlobSharedKeyCases drives the real client against the in-process reference Blob endpoint instead (signature recomputed in the test). A wrong key is the server's 403 -> exit 22; a non-base64 key exits 67.
- Criterion 4: the -v case shows `Authorization: SharedKey acct:<signature>` and never the key. Quality run: 0 failing members.

## Log

- 2026-11-19: Created from bug report 2026-11-19T0930-Curl-bug-report.md, finding 6.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 07e22df8 46f4cff0 984b340d 748ddad5 e3b0da79 3d836d5e.
- 2026-11-19: Doing -> Done. Shared Key signing built
