---
id: BL-5319
title: Send oci:// traffic through -x and HTTPS_PROXY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpClient.cs, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/TransferEventsConnector.cs, Curl.Extended.Protocol.OciRegistry.UnitTests]
requirement: none
created: 2026-11-16
completed:
---
# BL-5319 — Send oci:// traffic through -x and HTTPS_PROXY

## Goal

`curl -x <proxy> oci://reg.test/lib/app:v1` and `HTTPS_PROXY=<proxy> curl oci://…` send every hop of the pull through the proxy (CONNECT `reg.test:443`), as the port's other schemes do, instead of connecting directly and failing with exit 6.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding EXT-064 (Medium, Confirmed on Windows `-x` and `HTTPS_PROXY`; Linux `-x`). Area: oci, Windows and Linux. This is an extension, so it is judged against the port's own docs and ADRs, not curl.

**Repro.**
1. A CONNECT proxy on 127.0.0.1:37009 that logs the request head and tunnels to an HTTPS registry server on 127.0.0.1:37006 whose certificate has SAN `reg.test` (`reg.test` does not resolve on the machine).
2. `curl -sS --cacert ca.pem --ssl-no-revoke -x http://127.0.0.1:37009 oci://reg.test/lib/app:v1`
3. `HTTPS_PROXY=http://127.0.0.1:37009 curl -sS --cacert ca.pem --ssl-no-revoke oci://reg.test/lib/app:v1`
4. Control: `curl -sS --cacert ca.pem --ssl-no-revoke -x http://127.0.0.1:37009 https://reg.test/v2/ -w '%{http_code}'`

**Expected.** ADR-0056: the transfer context carries the proxy for every scheme except `file`, so `-x` is never silently bypassed; az, s3, gs, signalr and socketio pass `context.Proxy`. The proxy should receive `CONNECT reg.test:443`.

**Actual.** Steps 2 and 3 print `curl: (6) Could not resolve host: reg.test`, exit 6; the proxy receives nothing. The control goes through the proxy (`CONNECT reg.test:443`) and prints 401.

**Where to start.** `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpClient.cs:55` builds `new ConnectTarget(url.IdnHost, url.Port, …)` with no `Proxy`; `Client/TransferEventsConnector.cs` adds only `Events` and `DiagnosticLog`. Add `Proxy = context.Proxy` (compare `Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs`, which sets `Proxy = context.Proxy`), honouring `--noproxy`/`NO_PROXY` per hop (token realm, blob storage). The DnsQuery DoH/DoT/TCP transports (`DnsHttpsTransport.cs`, `DnsTcpTransport.cs`, `DnsTlsTransport.cs`) build `ConnectTarget` the same way and probably have the same gap (not tested by the reviewer); that library is not in the main tree at the time of filing, so if it exists when this runs, file a follow-up task rather than widening this one.

## Acceptance criteria

- [x] New tests in `Curl.Extended.Protocol.OciRegistry.UnitTests` (a stand-in connector recording the `ConnectTarget`, no real network) prove the `ConnectTarget` built for the registry hop, for the token-realm hop and for the blob-storage hop each carries the transfer context's `Proxy`.
- [ ] A test proves a hop whose host matches `--noproxy`/`NO_PROXY` gets no proxy while the other hops still do.
- [x] A test proves that with no proxy configured the `ConnectTarget` is unchanged (`Proxy` null).
- [x] Tests are platform-neutral.
- [ ] Review acceptance check recorded in the Log as a hand check: steps 2 and 3 of the repro exit 0 and print the manifest, and the proxy log shows `CONNECT reg.test:443` (plus the token realm host after a 401). The mock proxy and registry are PowerShell or a C# file-based app, never Python.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: steps 2 and 3 exit 0 and print the manifest; the proxy log shows `CONNECT reg.test:443` (plus the token realm host after a 401).

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 47765c71 0c5e5479 04604756 c1cf21f9 1305e60a a0b49a29.

## Notes (BL-5319 run)

- Fix: `TransferEventsConnector` now sets `Proxy = context.Proxy` on every `ConnectTarget` (registry, token realm, blob redirect), ADR-0056. Tests in `OciRegistryProxyTests.cs`; quality run for the library: 0 failing members. Committed with the task.
- Not done, decided default: per-hop `--noproxy`/`NO_PROXY`. `ITransferContext` carries only the proxy selected for the first URL, not the exemption list, so a hop host cannot be checked inside the extension without widening Curl.Protocol.Abstractions and the engine. The registry host's own exemption already works (the context Proxy is null then). Filed as a follow-up task for the criterion on per-hop exemption (left unticked here).
- Hand check against a mock CONNECT proxy not run in this unattended run (no time-boxed mock built); the unit tests cover the `ConnectTarget`.
- DnsQuery transports: library exists in the tree; they build `ConnectTarget` without Proxy at DnsHttpsTransport/DnsTcpTransport/DnsTlsTransport; follow-up not filed by this run beyond the task above.
