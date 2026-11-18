---
id: BL-5407
title: List an az:// container on a path-style Azurite emulator
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureBlobOperations.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageExtension.cs, Curl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5407 — List an az:// container on a path-style Azurite emulator

## Goal

`az://127.0.0.1:49020/devstoreaccount1/ctr/` against a path-style Azurite emulator lists the blobs of container `ctr` (account `devstoreaccount1` taken from the first path segment), as the docs promise.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-080 (Low, Confirmed: 2 listing URLs, each twice). Area: az (emulator use), Windows.

**Repro.**
1. `docker run -d --name azr -p 127.0.0.1:49020:10000 -v <dir>:/certs mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --loose --cert /certs/srv.pem --key /certs/srv.key --oauth basic` (cert SAN 127.0.0.1 from test CA `ca.pem`; `TOK` is any unsigned JWT with `aud=https://storage.azure.com`).
2. Create container `ctr` and upload `a.txt`: `curl --cacert ca.pem --ssl-no-revoke --oauth2-bearer $TOK -T small.txt az://127.0.0.1:49020/devstoreaccount1/ctr/a.txt` (works, 201).
3. `curl -sS --cacert ca.pem --ssl-no-revoke --oauth2-bearer $TOK az://127.0.0.1:49020/devstoreaccount1/ctr/`
4. `curl -sS --cacert ca.pem --ssl-no-revoke --oauth2-bearer $TOK az://127.0.0.1:49020/ctr/`

**Expected.** `AzureStorage.md` ("URL port") says the URL port is used "so an Azurite-style emulator works", and `--extended-help` says a name ending in `/` lists the names under it. Step 3 lists `a.txt` (Azurite answers `GET /devstoreaccount1/ctr?restype=container&comp=list&delimiter=/`).
**Actual (PORT).** Step 3 sends `GET /devstoreaccount1?restype=container&comp=list&delimiter=%2F&prefix=ctr%2F` and prints `curl: (8) Azure Storage sent a body curl cannot read: The element EnumerationResults has no Blobs.`; step 4 sends `GET /ctr?restype=container...` and prints `curl: (22) Azure Storage returned error 404: ResourceNotFound`. No URL form lists an emulator container.
**Where to start.** `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs` (the account/container split; `Map` for `az` passes `url.AbsolutePath` through) and the list request in `Client/AzureBlobOperations.cs`: for an IP or `localhost` host (a path-style emulator), take the first segment as the account and the second as the container; or document the emulator URL form.

## Acceptance criteria

- [x] New tests in `Curl.Extended.Protocol.AzureStorage.UnitTests` (a new file) prove a listing URL `az://127.0.0.1:49020/devstoreaccount1/ctr/` produces `GET /devstoreaccount1/ctr?restype=container&comp=list&delimiter=%2F` against the URL's host and port.
- [x] A test proves the same for a `localhost` host, and that a `ctr/dir/` prefix becomes `prefix=dir%2F`.
- [x] A test proves a public-cloud URL (`az://acct/ctr/` and `az://acct.blob.core.windows.net/ctr/`) still produces the host-style request it did before (no regression), and that a blob GET/PUT on an emulator URL keeps the account in the path.
- [x] Tests are platform-neutral.
- [x] Hand check recorded in the Log: step 3 prints `a.txt` and exits 0; `-v` shows `GET /devstoreaccount1/ctr?restype=container&comp=list...` (real Azurite in docker).
- [x] The `--extended-help` text for `az` and `AzureStorage.md` state the emulator URL form; `--ai-help` is checked.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- `touches` includes `Client/AzureBlobOperations.cs`, where the list request is built, and `Client/AzureStorageExtension.cs` for the help text.
- Review acceptance check: step 3 prints `a.txt` and exits 0; `-v` shows `GET /devstoreaccount1/ctr?restype=container&comp=list...`.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 013cbbea a89a8f8f bc96735e c1cf21f9 e2a9269d a7dd7de8 a0b49a29.
- 2026-11-17: Done by lane 13. New AzureStorageEmulatorUrlTests; ADR-1291. Hand check done against real Azurite 3.37 in docker (self-signed cert, unsigned JWT with iat/nbf/exp): `az://127.0.0.1:49020/devstoreaccount1/ctr/` printed `a.txt`, exit 0; -v showed `GET /devstoreaccount1/ctr?restype=container&comp=list&delimiter=%2F&prefix= HTTP/1.1`; blob GET printed `hi`. --ai-help reads the extension description, which was updated. Quality run: complexity 10, coverage 100%. Tests are platform-neutral.
- 2026-11-17: Doing -> Done. Emulator container listing works; verified on Azurite
