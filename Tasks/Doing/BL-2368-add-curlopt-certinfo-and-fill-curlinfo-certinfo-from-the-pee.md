---
id: BL-2368
title: Add CURLOPT_CERTINFO and fill CURLINFO_CERTINFO from the peer certificates
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2361]
touches: [Curl.Api.UnitLibrary, Curl.Api.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Libcurl, Libcurl.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2368 — Add CURLOPT_CERTINFO and fill CURLINFO_CERTINFO from the peer certificates

## Goal

With `CURLOPT_CERTINFO` set on a handle, `Info.CertInfo` after an `https` perform holds one list of `name:value` strings per peer certificate, as libcurl's `CURLINFO_CERTINFO` gives them.

## Context

Left by BL-2361 (ADR-0506 plan phase 2): `Curl.Api`'s `EasyTlsOptions` has no `CURLOPT_CERTINFO` yet. `TransferReport.PeerCertificates` already holds the chain's DER bytes; `Curl.Output`'s `PeerCertificateText` words them as `%{certs}` prints them, and each of its lines becomes one `name:value` string. Match the platform's curl (Schannel build on Windows, OpenSSL build elsewhere) and measure real curl's `%{certs}` output before pinning names.

## Acceptance criteria

- [ ] `EasyTlsOptions.CertInfo` exists with libcurl's default (off), and the `Libcurl` setopt maps `CURLOPT_CERTINFO` to it.
- [ ] A `ManagedEasyTransferTests` or `TransferInfoFromReportTests` test shows the lists filled under the option and empty without it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library <every library changed>` reports 0 failing members.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
