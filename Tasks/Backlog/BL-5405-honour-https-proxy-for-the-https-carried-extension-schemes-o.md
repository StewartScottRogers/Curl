---
id: BL-5405
title: Honour HTTPS_PROXY for the HTTPS-carried extension schemes oci, s3, gs, az and signalrs
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/ProxySelector.cs, Curl.Core.UnitTests, Curl.Engine.UnitTests, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciRegistryExtension.cs, Curl.Extended.Protocol.S3.UnitLibrary/Client/S3Extension.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsExtension.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageExtension.cs, Curl.Extended.Protocol.SignalR.UnitLibrary/SignalRExtension.cs, Documentation/Protocols.md]
requirement: none
created: 2026-11-17
completed:
---
# BL-5405 — Honour HTTPS_PROXY for the HTTPS-carried extension schemes oci, s3, gs, az and signalrs

## Goal

The extension schemes that always speak HTTPS (`oci`, `s3`, `gs`, `az`, `signalrs`) use `https_proxy` / `HTTPS_PROXY` when no `<scheme>_proxy` variable is set, exactly as `wss` does, so a user who sets only `HTTPS_PROXY` goes through the proxy.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding EXT-076 (Low, Confirmed: Windows 2 runs, oci on Linux once). Area: oci, s3, gs, az, signalrs proxy selection.

**Repro.**
1. A CONNECT proxy on 127.0.0.1:43009 that logs request heads and tunnels `reg.test:443` to an HTTPS registry mock (certificate SAN `reg.test`; `reg.test` does not resolve).
2. `HTTPS_PROXY=http://127.0.0.1:43009 curl -sS -m 10 --cacert ca.pem --ssl-no-revoke oci://reg.test/lib/app:v1` (also `https_proxy=`).
3. The same for `s3://bkt.test/k`, `gs://bkt/k`, `az://acct.blob.core.windows.net/c/x`, `signalrs://reg.test/hub`.
4. Controls: `HTTPS_PROXY=... curl https://reg.test/v2/` and `HTTPS_PROXY=... curl wss://reg.test/x` use the proxy; `ALL_PROXY=...` or `OCI_PROXY=...` with the oci command uses it.
**Expected.** These schemes always speak HTTPS (`--extended-help` for oci says "over HTTPS"); for consistency with `wss` (which maps to `https_proxy`) they honour `HTTPS_PROXY`.
**Actual (PORT).** oci and signalrs give `curl: (6) Could not resolve host: reg.test`; s3, gs and az connect directly. The proxy receives nothing.
**Where to start.** `Curl.Core.UnitLibrary/ProxySelector.cs`, `ProxyVariableNames` (lines ~203-225): it appends `https_proxy` then `HTTPS_PROXY` for `wss` only. `Curl.Engine.UnitLibrary/TransferProxySelection.cs` calls `TrySelect`/`TryFindProxyVariable`. Check the extension libraries (`Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/HopNoProxyMatcher.cs` and the S3, Google Cloud Storage, Azure Storage and SignalR libraries) pick their proxy through the same selector and do not read variables themselves.

**Decision (Claude, under Stewart's delegation).** Honour `HTTPS_PROXY` for the HTTPS-carried extension schemes, for consistency with `wss`; `<scheme>_proxy` and `ALL_PROXY` keep their precedence. Record it in a new ADR "Decided by Claude under Stewart's delegation" that extends ADR-0024, and keep the rule limited to extension schemes so curl's own schemes (and `curl -V`) are untouched.

## Acceptance criteria

- [ ] New tests in `Curl.Core.UnitTests` (a new file) prove `TryFindProxyVariable` for `oci`, `s3`, `gs`, `az` and `signalrs` returns `https_proxy`, then `HTTPS_PROXY`, after `<scheme>_proxy` and its upper-case form and before `all_proxy`.
- [ ] A test proves `<scheme>_proxy` (for example `oci_proxy`) and `ALL_PROXY` still behave as before, and that `http_proxy` alone is not used for these schemes.
- [ ] A test proves `ftp`, `smtp` and the other non-HTTPS schemes do not read `https_proxy` (no regression).
- [ ] A test in `Curl.Engine.UnitTests` proves a transfer for an `oci://` URL with only `HTTPS_PROXY` set selects that proxy, and `--noproxy` / `NO_PROXY` still disable it.
- [ ] The ADR is filed and the proxy rule is stated in `Documentation/Protocols.md` and in the `--extended-help` text of each of the five schemes; `--ai-help` is checked and kept right if it describes extension proxies.
- [ ] Tests are platform-neutral.
- [ ] Hand check recorded in the Log: step 2 exits 0 and the proxy logs `CONNECT reg.test:443`; each step-3 command makes the proxy log a CONNECT.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- The `.UnitLibrary` quality gates (100% line and branch coverage, complexity at most 10) apply to the changed selector.
- Review acceptance check: step 2 exits 0 and the proxy logs `CONNECT reg.test:443`; each step-3 command makes the proxy log a CONNECT. (Or: the docs state the rule, and an ADR records the choice.)

## Log

- 2026-11-17: Created.
