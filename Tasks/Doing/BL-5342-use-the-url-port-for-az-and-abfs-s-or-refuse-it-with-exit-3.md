---
id: BL-5342
title: Use the URL port for az:// and abfs[s]:// or refuse it with exit 3
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageEndpoint.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Requests/AzureStorageUrlMapping.cs, Curl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-5342 — Use the URL port for az:// and abfs[s]:// or refuse it with exit 3

## Goal

An `az://`, `abfs://` or `abfss://` URL that names a port connects to that port (so an Azurite-style `az://127.0.0.1:10000/c/x` works), instead of the port being silently dropped and the connection going to 443.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-070 (Low, Confirmed, 3 URLs; silent wrong endpoint, so a SAS or bearer token goes to port 443). Area: az, abfs, abfss; Windows. This is an extension, so it is judged against the port's own docs and ADRs, not curl.

**Repro.** A CONNECT logging proxy on 127.0.0.1:37004 (PowerShell or a C# file-based app, never Python), then:
- `curl -sS -x http://127.0.0.1:37004 --cacert ca.pem --ssl-no-revoke az://acct.blob.core.windows.net:8443/c/x`
- `curl -sS -x http://127.0.0.1:37004 --cacert ca.pem --ssl-no-revoke az://127.0.0.1:10000/c/x`
- `curl -sS -x http://127.0.0.1:37004 --cacert ca.pem --ssl-no-revoke "abfss://fs@acct.dfs.core.windows.net:10000/c/x"`

**Expected.** Consistency with the port's other schemes (s3/gs endpoints, oci `registry[:port]`, signalr): use the URL's port, or refuse it with exit 3. Neither `--extended-help` nor `AzureStorage.md` says the port is dropped.

**Actual.** The proxy logs `CONNECT acct.blob.core.windows.net:443`, `CONNECT 127.0.0.1:443` and `CONNECT acct.dfs.core.windows.net:443`.

**Where to start.** `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs`: `ConnectTarget target = new(endpoint.Host, HttpsPort, true)`; `AzureStorageEndpoint` has no port and `Requests/AzureStorageUrlMapping.cs` drops `url.Port`. The simplest drop-in choice is to use the URL's port (add a port to `AzureStorageEndpoint`, default 443, keep it in the signed canonical resource and `Host` header only as the service requires). If a port cannot be honoured, refuse it with exit 3 and a message naming the port. Record whichever is chosen in `AzureStorage.md` (Claude's decision under Stewart's delegation; add an ADR only if the choice is not obvious).

## Acceptance criteria

- [ ] New tests in `Curl.Extended.Protocol.AzureStorage.UnitTests` (a stand-in connector recording the `ConnectTarget`) prove that `az://acct.blob.core.windows.net:8443/c/x`, `az://127.0.0.1:10000/c/x` and `abfss://fs@acct.dfs.core.windows.net:10000/c/x` produce a `ConnectTarget` with host and port from the URL (or, if refused, exit 3 with a message naming the port).
- [ ] A test proves a URL with no port still connects to 443, and that the `Host` header and the request authorization (SAS or shared-key canonical resource) are correct for a non-default port.
- [ ] `AzureStorage.md` states how a URL port is treated.
- [ ] Tests are platform-neutral.
- [ ] Review acceptance check recorded in the Log as a hand check: `curl -x http://127.0.0.1:37004 … az://127.0.0.1:10000/c/x` makes the proxy log `CONNECT 127.0.0.1:10000` (or curl exits 3 with a message that a port is not supported).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: `curl -x http://127.0.0.1:37004 … az://127.0.0.1:10000/c/x` makes the proxy log `CONNECT 127.0.0.1:10000` (or curl exits 3 with a message that a port is not supported).
- Add `Documentation/` paths to `touches` when the doc is found (the AzureStorage doc's exact path was not confirmed at filing).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: eb489812 c3bc75b3 ee69b5cf c1cf21f9 cb56750e a0b49a29.
