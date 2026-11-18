---
id: BL-5480
title: Fix fitness finding EXT-086: NO_PROXY / --noproxy for s3, gs, az and abfs are matched against the bucket or account name, not the host actually connected to
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary/ProxySelector.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5480 — Fix fitness finding EXT-086: NO_PROXY / --noproxy for s3, gs, az and abfs are matched against the bucket or account name, not the host actually connected to

## Goal

EXT-086 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: NO_PROXY / --noproxy for s3, gs, az and abfs are matched against the bucket or account name, not the host actually connected to. Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Medium (silent wrong route: the common `NO_PROXY=localhost,127.0.0.1` sends emulator traffic, including signed requests, through the corporate proxy; entries for real cloud hosts have no effect; a bucket name can match an unrelated entry and skip the proxy)
- **Confidence:** Confirmed (2 runs each)
- **Protocol/area:** s3, gs, az, abfs, abfss proxy selection
- **Repro:** a proxy on 127.0.0.1:47051 that logs each request head and tunnels CONNECT; SeaweedFS S3 (`chrislusf/seaweedfs`) on http 127.0.0.1:47040 with bucket `bkt` holding `a.txt` = `hello`; fake-gcs-server on https 127.0.0.1:47020 (test CA `ca.pem`) with bucket `gbkt`.
  1. `HTTPS_PROXY=http://127.0.0.1:47051 NO_PROXY=localhost,127.0.0.1 AWS_ENDPOINT_URL=http://127.0.0.1:47040 AWS_ACCESS_KEY_ID=AKIDB3TEST AWS_SECRET_ACCESS_KEY=b3secretkey curl -sS s3://bkt/a.txt`
  2. The same with `--noproxy 127.0.0.1` instead of `NO_PROXY`.
  3. `HTTPS_PROXY=http://127.0.0.1:47051 NO_PROXY=127.0.0.1 STORAGE_EMULATOR_HOST=https://127.0.0.1:47020 curl -sS --cacert ca.pem --ssl-no-revoke gs://gbkt/a.txt`
  4. `HTTPS_PROXY=http://127.0.0.1:47051 NO_PROXY=amazonaws.com curl -sS s3://bkt/a.txt`; also `gs://gbkt/a.txt` with `NO_PROXY=googleapis.com` (or `--noproxy googleapis.com`), `az://acct/ctr/x` with `NO_PROXY=windows.net`, `abfss://fs@acct/x` with `ALL_PROXY=http://127.0.0.1:47051 NO_PROXY=windows.net`.
  5. Control: `NO_PROXY=bkt` (or `gbkt`, `acct`) with step 4's commands makes them go direct.
- **Expected:** ADR-1297: "`--noproxy` and `NO_PROXY` apply as before"; the port's own rule for oci hops (ADR-1290, `HopNoProxyMatcher`) decides by the host actually connected to. Steps 1–4 connect directly.
- **Actual (PORT):** the proxy logs `CONNECT 127.0.0.1:47040`, `CONNECT 127.0.0.1:47020`, `CONNECT bkt.s3.us-east-1.amazonaws.com:443`, `CONNECT storage.googleapis.com:443`, `CONNECT acct.blob.core.windows.net:443`, `CONNECT acct.dfs.core.windows.net:443`. Only the bucket or account name (step 5) exempts a request.
- **Likely location:** `Curl.Core.UnitLibrary/ProxySelector.cs` `IsReachedDirectly` uses `HostName(url)` (the URL host); the endpoint mapping happens later in `S3Endpoint.For`, `GcsEndpoint.For` and `AzureStorageUrlMapping`. Match against the mapped endpoint host (as `HopNoProxyMatcher` does for oci), or let the scheme handler supply the effective host.
- **Acceptance check:** steps 1–4 leave the proxy log empty and behave as direct connections (step 1 prints `hello`, exit 0); step 5 goes through the proxy (`CONNECT bkt.s3.us-east-1.amazonaws.com:443`).

## Acceptance criteria

- [x] steps 1–4 leave the proxy log empty and behave as direct connections (step 1 prints `hello`, exit 0); step 5 goes through the proxy (`CONNECT bkt.s3.us-east-1.amazonaws.com:443`).
- [x] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

ProxySelector now matches NO_PROXY and --noproxy against the effective host: s3 endpoint (AWS_ENDPOINT_URL_S3, AWS_ENDPOINT_URL, else bucket.s3.region.amazonaws.com), gs (STORAGE_EMULATOR_HOST, else storage.googleapis.com), az (account.blob.core.windows.net, local hosts as written), abfs and abfss (account.dfs.core.windows.net). The bucket or account name no longer exempts (step 5 goes via proxy, per the criterion). The mapping is duplicated in Core because Core cannot reference the protocol libraries. Tests in ProxySelectorStorageSchemeNoProxyTests; the Core quality run is clean.

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding EXT-086 (Medium).
- 2026-11-18: Backlog -> Doing. Criteria at claim: 1033c7c6 69116afe 3d836d5e.
- 2026-11-18: Doing -> Done. NO_PROXY and --noproxy for s3, gs, az and abfs are matched against the host actually connected to
