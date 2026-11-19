---
id: BL-5486
title: Fix fitness finding EXT-092: abfs:// and abfss:// ignore HTTPS_PROXY, although az:// in the same extension honours it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/ProxySelector.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5486 — Fix fitness finding EXT-092: abfs:// and abfss:// ignore HTTPS_PROXY, although az:// in the same extension honours it

## Goal

EXT-092 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: abfs:// and abfss:// ignore HTTPS_PROXY, although az:// in the same extension honours it. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low (with only `HTTPS_PROXY` set, the request bypasses the proxy)
- **Confidence:** Confirmed (2 runs)
- **Protocol/area:** abfs, abfss proxy
- **Repro:** a logging CONNECT proxy on 127.0.0.1:47051. `HTTPS_PROXY=http://127.0.0.1:47051 curl -sS -m 5 "abfss://fs@acct/x"` (also `abfs://`). Controls: `ALL_PROXY=…` or `abfss_proxy=…` use the proxy; `HTTPS_PROXY=… curl -sS -m 5 az://acct/c/x` uses it.
- **Expected:** AzureStorage.md: "The request is always HTTPS" for az, abfs, abfss; ADR-1297 and `Documentation/Protocols.md`: HTTPS-carried extension schemes use `https_proxy`/`HTTPS_PROXY`. The proxy logs `CONNECT acct.dfs.core.windows.net:443`.
- **Actual (PORT):** a direct connection to `acct.dfs.core.windows.net:443` (here `curl: (60) … untrusted root`); the proxy log is empty.
- **Likely location:** `Curl.Core.UnitLibrary/ProxySelector.cs` `HttpsProxySchemes = ["wss", "oci", "s3", "gs", "az", "signalrs"]` lacks `abfs` and `abfss`; also update the azure-storage `--extended-help` sentence that names only `az_proxy`.
- **Acceptance check:** the command makes the proxy log `CONNECT acct.dfs.core.windows.net:443`; `http_proxy` alone still connects directly.

## Acceptance criteria

- [x] the command makes the proxy log `CONNECT acct.dfs.core.windows.net:443`; `http_proxy` alone still connects directly.
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added abfs and abfss to HttpsProxySchemes; also edited the azure-storage extended-help sentence (AzureStorageExtension.cs, outside touches, one string). Quality run for Curl.Core: 0 failing. Linux/macOS: platform-neutral change.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-092 (Low).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 5c905248 69116afe 3d836d5e.
