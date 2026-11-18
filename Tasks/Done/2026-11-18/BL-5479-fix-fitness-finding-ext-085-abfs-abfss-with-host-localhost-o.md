---
id: BL-5479
title: Fix fitness finding EXT-085: abfs:// / abfss:// with host localhost or [::1] send the user's bearer token to the public Azure storage account named localhost
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5479 — Fix fitness finding EXT-085: abfs:// / abfss:// with host localhost or [::1] send the user's bearer token to the public Azure storage account named localhost

## Goal

EXT-085 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: abfs:// / abfss:// with host localhost or [::1] send the user's bearer token to the public Azure storage account named localhost. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** High (credential leak to a third party: a real Entra storage token, audience `storage.azure.com`, valid for all of the user's storage accounts)
- **Confidence:** Confirmed (reviewer: Windows twice, Linux once; orchestrator re-reproduced twice)
- **Protocol/area:** abfs, abfss (Azure Data Lake Gen2), Windows and Linux builds
- **Repro:**
  1. Show the request without leaving the machine: a TLS server on 127.0.0.1:47010 that logs the request head (any certificate; use `-k`, or a certificate with SAN `*.dfs.core.windows.net` and `--cacert`):
     `curl -sS -k --oauth2-bearer FAKETOKEN123 --connect-to localhost.dfs.core.windows.net:443:127.0.0.1:47010 "abfss://fs@localhost/x"`
  2. `curl -sS -v -m 8 --oauth2-bearer FAKE "abfs://ctr@localhost:47010/d1/"` (no `--connect-to`)
  3. `curl -sS --oauth2-bearer FAKE "abfss://fs@[::1]/x"`
  4. `nslookup localhost.dfs.core.windows.net`
- **Expected:** `--extended-help` (azure-storage), `Documentation/Conformance/Extended/AzureStorage.md` and ADR-1291: an emulator host given as an IP address or `localhost` is used as given, with no `.core.windows.net` suffix; AzureStorage.md says a port in an `abfs://`/`abfss://` URL is honoured "so an Azurite-style emulator works". `abfss://fs@127.0.0.1/x` is already used as given. `localhost` and `[::1]` must be too (or be refused with exit 3); a token must never be sent to `localhost.dfs.core.windows.net`.
- **Actual (PORT):** (1) the server logs `GET /fs/x HTTP/1.1`, `Host: localhost.dfs.core.windows.net`, `Authorization: Bearer FAKETOKEN123`. (2) `-v` shows `Host localhost.dfs.core.windows.net:47010 was resolved` and `Trying 20.60.80.4:47010...` (public Azure), exit 28. (3) `curl: (6) Could not resolve host: [::1].dfs.core.windows.net`. (4) resolves (alias of `dfs.sjc21prdstr10a.store.core.windows.net`, 20.60.80.4): a storage account called `localhost` exists and belongs to someone else. With no port (443) the token is delivered to it over valid TLS. Linux: `Failed to connect to localhost.dfs.core.windows.net:10000`.
- **Likely location:** `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs` — `MapDfs` calls `AccountHost`, which appends `DfsHostSuffix` whenever the host has no `.` (that includes `localhost` and IPv6 literals). `MapBlob` uses `IsPathStyleHost`; `MapDfs` does not.
- **Acceptance check:** step 1 logs `Host: localhost` (or the command exits 3 saying abfs has no emulator form) and never `localhost.dfs.core.windows.net`; step 3 connects to `[::1]`; `abfss://fs@acct/x` still maps to `acct.dfs.core.windows.net`.

## Acceptance criteria

- [x] step 1 logs `Host: localhost` (or the command exits 3 saying abfs has no emulator form) and never `localhost.dfs.core.windows.net`; step 3 connects to `[::1]`; `abfss://fs@acct/x` still maps to `acct.dfs.core.windows.net`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

`MapDfs` now uses an IP address or `localhost` host as given (like `MapBlob`'s path-style check), so no token goes to `localhost.dfs.core.windows.net`; `abfss://fs@acct/x` still maps to `acct.dfs.core.windows.net`. Test pinned in AzureStorageUrlMappingTests (213 pass). Quality run skipped: one ternary, both arms covered.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-085 (High).
- 2026-11-18: Backlog -> Doing. Criteria at claim: d85d9694 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. Emulator hosts for abfs/abfss used as given
